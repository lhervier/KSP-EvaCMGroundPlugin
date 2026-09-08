# Détection « la pièce est dans le sol » — conception, impasses, mesures

Détail de la mécanique de détection et de tout ce qui a été essayé autour. Le `CLAUDE.md` du projet
n'en garde que le résumé ; **venir ici avant de toucher à `GetReachablePosition`,
`GetCastDistance`, `IsPoseInGround` ou au balayage**, sinon on refait les impasses documentées plus
bas — elles ont coûté une séance entière (2026-09-03).

## Comment ça marche aujourd'hui

Un déplacement n'est plus **refusé**, il est **tronqué** : la pièce avance jusqu'au contact réel,
moins la garde au sol. Trois étages, du moins cher au plus précis.

1. **Détecter** — `GetCastDistance` balaie la forme du collider (`Physics.BoxCastAll` /
   `SphereCastAll` / `CapsuleCastAll`) le long du déplacement. Il ne répond qu'à « y a-t-il quelque
   chose sur le trajet ». Rien → le déplacement est accordé entier, en une requête : c'est le cas
   courant et il ne coûte rien.
2. **Encadrer** — sondages répartis sur une fenêtre, en testant réellement chaque pose
   (`IsPoseInGround`, donc `ComputePenetration`).
3. **Affiner** — dichotomie (`BISECTION_STEPS = 12`) entre la dernière pose dégagée et la première
   pose dans le sol. Les deux bornes sont testées pour de vrai : la pièce ne peut ni s'arrêter trop
   haut, ni glisser au travers.

Le **balayage pose par pose subsiste, mais pour la seule rotation** : un cast va en ligne droite et
ne sait pas l'exprimer, alors qu'un collider éloigné de l'origine parcourt un arc qui enjambe une
surface comme une translation. Son parcours est borné par l'angle, donc `MAX_SWEEP_STEPS` n'y est
plus un trou.

### Les cinq invariants à ne pas casser

- **Le pas de sondage se déduit de la fenêtre, jamais d'une dimension de collider.** L'épaisseur
  d'une pièce ne dit rien de la distance sur laquelle elle chevauche encore le sol. Mesuré sur
  l'Oscar-B : un collider **haut de 0,35 m** est franchi en **moins de 0,19 m**, et un pas tiré de sa
  boîte englobante (0,31 m) l'enjambait proprement. D'où `step = window / MAX_SWEEP_STEPS`.
- **La pose d'arrivée est toujours sondée.** Les sondages sont *répartis* sur l'intervalle
  (`Mathf.Lerp` sur `probes`), pas comptés depuis son début : sinon un déplacement plus court qu'un
  pas n'est jugé que sur sa pose de départ, qui est dégagée par construction. C'est exactement ce qui
  laissait passer un déplacement de 0,186 m avec un pas de 0,234 m.
- **Au-delà de la fenêtre, rien n'est accordé.** Quand le cast a trouvé quelque chose, le
  déplacement est tronqué à `firstTouch + window` même si le joueur en demande plus : au-delà, aucune
  pose n'a été regardée (une autre pente, un bâtiment). Les événements arrivant toutes les ~25 ms, la
  pièce avance quand même vite.
- **Un cast part toujours en arrière de la pièce.** Il rapporte ce qu'il chevauche déjà comme un hit
  à distance nulle et ne dit rien de ce qui suit. `GetCastDistance` recule donc son origine d'une
  longueur de boîte (plus la garde au sol) et retranche autant du résultat. **Ne pas « corriger » en
  filtrant les hits à distance nulle** : c'est ce qu'on avait fait, et ça neutralise la détection
  précisément au contact du sol, là où elle sert.
- **On ne tronque que depuis une pose de départ dégagée** (ajouté le 2026-09-08). Les deux étages
  mesurent un déplacement *depuis* une pose qu'ils supposent hors du sol — la dichotomie encadre le
  point d'arrêt entre le départ et la première pose enterrée, le balayage de rotation part de
  `step = 1`. Une pièce peut pourtant démarrer enterrée : attachée sur un nœud, détail du terrain
  changé sous elle, pose suivie d'avant un changement de scène. Les deux issues sont mauvaises et
  aucune n'est rattrapable en aval :
  - si le cast rapporte le chevauchement initial (c'est le cas, grâce au recul d'origine ci-dessus),
    `firstTouch = 0`, le premier sondage est dans le sol, `free` reste à 0 : la dichotomie converge
    sur la pose de départ et la pièce est **figée dans toutes les directions, y compris vers le
    haut** — impossible de la déterrer ;
  - s'il ne le rapporte pas, « rien sur le trajet » et **tout le déplacement est accordé, dans le
    sol**.

  D'où le test préalable dans `OnEditorPartEvent` (`IsPoseInGroundAt` sur
  `previousPosition`/`previousRotation`) : départ enterré → **déplacement accordé entier**, la pièce
  reste manipulable, et la troncature reprend dès que la pose sur laquelle elle est lâchée est
  dégagée. Ne pas remplacer ce court-circuit par une troncature « intelligente » (n'autoriser que ce
  qui sort du sol) : le mod ne sait pas où est la sortie, et le joueur, si.

  ⚠️ Ce test hérite de l'angle mort des colliders concaves ci-dessous : une pièce entièrement sous la
  peau du terrain n'est vue enterrée par personne, ni par ce test ni par la détection. Il couvre le
  cas qui figeait la pièce, pas celui qu'on ne sait pas détecter.

## Pièges de géométrie déjà payés

Quatre corrections de géométrie, toutes **silencieuses** : aucune n'a jamais produit d'erreur ni de
ligne de log, chacune ne se voit qu'à la pose finale — la pièce s'enfonce, ou s'arrête trop tôt.
Aucune ne se déduit de la documentation Unity ; les redécouvrir coûte une séance.

### Deux variables à ne jamais refusionner

Corrigé le 2026-09-02. `GetBoxColliders`, `GetCapsuleColliders`, `GetSphereColliders` et
`GetMeshColliders` séparent :

- `volumeWorldCenter` = `transform.TransformPoint(collider.center)` + décalage sol → pour la phase
  large (`Physics.Overlap*`) ;
- `transformWorldPosition` = `transform.position` + décalage sol → pour `GetPenetratingColliders`,
  donc `Physics.ComputePenetration`.

Le décalage sol (`GetGroundOffsetVector`) s'applique **aux deux**. Avant le correctif, une seule
variable `center` servait aux deux rôles, et la phase large cherchait au mauvais endroit pour tout
collider décalé de l'origine de son transform : la pièce se posait dans le sol sans la moindre
erreur. `GetMeshColliders` avait déjà le bon schéma et sert de modèle.

### `Collider.bounds` est déjà en espace monde

Corrigé le 2026-09-03. `GetMeshColliders` bâtit son volume de recherche sur `meshCollider.bounds` :
ses extents sont **déjà** en unités monde (pas de `lossyScale` à appliquer) et ses axes sont **déjà**
ceux du monde, d'où `Quaternion.identity` en rotation. Remultiplier par `lossyScale` gonflait la
boîte (×20 sur certaines pièces, ×0,5 sur d'autres) et lui appliquer `transform.rotation` permutait
ses dimensions entre axes. Le récit de la découverte et les mesures sont plus bas, § « Impasses ».

### Seuls les colliders solides comptent — des deux côtés

Corrigé le 2026-09-08. Un **trigger** est un volume qu'un module surveille, pas une surface sur
laquelle on pose quelque chose, et rien ne l'oblige à rester sur le calque « Part Triggers » :
`ModuleRobotArmScanner` accroche au bras une `SphereCollider` de **4 m de rayon**, `isTrigger = true`,
sur le calque **Local Scenery**
([`ModuleRobotArmScanner.cs:548-556`](file:///d:/ksp-decompiled/Expansions.Serenity/ModuleRobotArmScanner.cs#L548)).
Un collider **désactivé** (`enabled == false`) est, lui, hors de la scène physique — mais
`GetComponentsInChildren<Collider>()` le rend quand même.

Le mod ne filtrait ces triggers que **du côté touché** (les colliders rendus par la phase large et par
les casts), et par un cas particulier sur le nom `rangeTrigger`. Du côté **mobile** — la liste des
colliders de la pièce déplacée — rien : déplacer une pièce portant un tel trigger l'arrêtait **4 m
au-dessus du sol**.

Depuis, les deux côtés appliquent la même règle, et le cas particulier `rangeTrigger` a disparu :

- côté mobile, `GetSolidColliders(part)` écarte `!enabled || isTrigger` à la construction de la liste ;
- côté touché, les quatre `Physics.Overlap*` passent `QueryTriggerInteraction.Ignore` — les casts le
  faisaient déjà, d'où un filtre `rangeTrigger` qui y était de toute façon mort.

⚠️ Le `LAYER_MASK` ne suffit pas : il exclut le calque « Part Triggers », mais un trigger peut vivre
ailleurs, et c'est justement le cas de celui-là.

### La garde au sol est une vraie distance — appliquée **une seule fois**, verticalement

Depuis la troncature, le paramètre n'est plus un biais sur un test booléen : la pièce s'immobilise
précisément à cette hauteur du sol. À 0 elle se pose au contact franc — une marge strictement
positive garantit en revanche que la pose enregistrée est franchement non chevauchante malgré les
arrondis. **Défaut : 0 depuis le 2026-09-08** (il était de 1 cm) ; plafond 10 cm, curseur toujours
en place. Lionel veut jouer sans marge et jugera sur pièce s'il faut la rétablir.

⚠️ **Elle s'applique en un seul endroit : `GetGroundOffsetVector`**, qui descend le volume de test le
long de la verticale locale (`-up`) avant chaque `Physics.Overlap*` et chaque `ComputePenetration`.
La dichotomie rend donc déjà une pose dégagée de cette hauteur, et `GetReachablePosition` **ne la
retranche plus** de la distance parcourue. Corrigé le 2026-09-08 : elle l'était, ce qui la comptait
**deux fois** — une fois à la verticale, une fois le long du déplacement — pour une garde effective
entre 1× et 2× le réglage selon l'angle. Sans conséquence observable au défaut actuel (0), mais
mortel dès que le curseur bouge. Ce correctif clôt du même coup l'ancien « chantier ouvert » sur
l'axe : la garde est désormais **verticale**, comme son nom le dit.

Reste vrai, et volontaire : le `+ GroundOffset` du `backoff` de `GetCastDistance` n'a rien à voir. Il
recule l'origine du cast et se retranche du résultat, donc il s'annule ; il ne fait qu'offrir de la
place au cast pour démarrer.

## Angles morts connus

### Les colliders concaves sont invisibles

`ComputePenetration` ne rend rien entre **deux maillages concaves** : PhysX ne calcule pas de
distance de séparation dans ce cas. Le terrain l'est toujours
([`PQSMod_QuadMeshColliders.cs:52`](file:///d:/ksp-decompiled/PQSMod_QuadMeshColliders.cs#L52)), donc
toute pièce dont le `MeshCollider` porte `convex = 0` échappe entièrement au mod. Ça marche avec le
réservoir parce que *lui* est convexe.

Portée mesurée (2026-09-03, stock + Restock) : **7 pièces transportables**, pas une famille entière —
`Size_1_5_Cone`, `fairingSize1`, `fairingSize1p5`, `fireworksLauncherSmall`, `largeAdapter2`,
`solarPanelSP10C`, plus le **point d'ancrage Coll-O-Tron** (absent du balayage des `ModuleCargoPart`,
il passe par `ModuleGroundPart`).

Conséquence : rien ne retient ces pièces, elles peuvent être posées dans le sol sans que le mod
réagisse.

⚠️ **Ne pas leur imputer les structures qui montent au rechargement.** On l'a cru, à tort : ce
symptôme a une tout autre cause, sans rapport avec le placement — voir
[CLAUDE-rechargement.md](CLAUDE-rechargement.md).

Rendre son collider convexe **n'est pas une solution** : il est *géométriquement* concave (241
sommets, 136 triangles, jusqu'à 0,83 m de creux — les vides entre les bras de l'embase). Son
enveloppe convexe serait une galette pleine, et comme le collider est celui du jeu, la modification
vaudrait pour tous les vaisseaux. Basculer `MeshCollider.convex` autour de chaque test est également
exclu : ça recompile le maillage de collision.

Piste si on décide de traiter : échantillonner les sommets du maillage du collider et, pour chacun,
lancer un rayon vers le bas depuis quelques mètres au-dessus — la surface touchée *au-dessus* du
point signe un point enterré. Exact (vrai maillage, bâtiments compris), borné, et sans toucher au
jeu. **Non implémenté.** Et avant de le lancer, trancher une question de conception : l'ancre est faite
pour être plantée dans le sol, donc l'en empêcher casserait peut-être la pièce. Il faudrait alors
l'exclure explicitement plutôt que la contraindre.

### Le pré-test d'altitude a été supprimé

`IsPoseInGround` ne fait plus que boucler sur les colliders. Le pré-test sur
`part.transform.position` contre `TerrainAltitude` a été retiré le 2026-09-03 : il bornait la
descente à partir d'un point sans rapport avec la forme de la pièce (17,5 cm sur l'Oscar-B, dont
l'origine est au centre), ce qui interdisait de poser au plus près. La troncature couvre le même
besoin, et mieux — elle voit les bâtiments et les statiques que le champ PQS ignore.

⚠️ La « piste laissée ouverte » de l'ancienne version du `CLAUDE.md`, qui proposait d'étendre ce
pré-test au centre des colliders, est **caduque**.

## Impasses — ne pas les refaire

- **`Rigidbody.SweepTest` / `SweepTestAll`.** C'est le seul balayage qui respecte la vraie forme des
  colliders, et il est **inutilisable ici** : `part.rb` est **null** pendant `PartOffsetting`
  (constaté sur 64 événements, `sweep skipped: rb=NULL`). Le rigidbody créé en
  [`EVAConstructionModeEditor.cs:1494`](file:///d:/ksp-decompiled/EVAConstructionModeEditor.cs#L1494)
  l'est dans le chemin de **saisie**, pas de déplacement. D'où les casts par collider, qui ne
  demandent aucun rigidbody — au prix d'une boîte englobante en doublure pour les `MeshCollider`,
  Unity ne balayant pas de maillage arbitraire.
- **Se fier à `lossyScale` sans le mesurer** (la règle : § « `Collider.bounds` est déjà en espace
  monde » ci-dessus). Avant correctif, la phase large multipliait les extents de `bounds` par
  `lossyScale` — alors que `Collider.bounds` est déjà en espace monde. Sur le Basic
  Fin, `lossyScale` vaut **20** (échelle enfouie dans la hiérarchie du `.mu`, pas dans
  `rescaleFactor` ni dans le nœud `MODEL`) : la boîte était 20× trop grande et masquait tout. Sur
  l'Oscar-B elle vaut **(0.500, 0.187, 0.500)**, donc trop petite dans toutes les orientations.
- **Le cliquet sur `previousPart`.** La branche `part != this.previousPart` remet `previousPosition`
  sur la position courante ; on a soupçonné qu'un événement portant sur une autre pièce en plein
  glissement réinitialisait la référence. **Mesuré : zéro occurrence** pendant un glissement. Piste
  fermée.
- **Le clamp d'altitude au chargement** comme cause d'une remontée — voir
  [CLAUDE-rechargement.md](CLAUDE-rechargement.md), mesuré à zéro.

## Méthode — ce qui a fait perdre le plus de temps

- **Vérifier quelle DLL *tourne*, pas laquelle est installée.** Comparer l'empreinte de
  `GameData/.../EvaCMGroundMod.dll` à celle de `Output/bin/` ne prouve rien : un `build.bat`
  ultérieur écrase la seconde. Et une DLL remplacée sous un KSP en cours d'exécution **n'est pas
  rechargée**. Le seul contrôle fiable : chercher dans `KSP.log` une ligne que seule la nouvelle
  version émet, et vérifier que `[GroundFix] Plugin started` est postérieur à l'installation.
- **Compter les sessions de construction avant de lire le log.** `grep -c "Starting Fix"`. Une
  manipulation ratée suivie d'un rechargement laisse **deux** sessions dans le même fichier, et lire
  la mauvaise mène à des conclusions inventées de toutes pièces (des `PartDragging` de 1,1 m pris
  pour le régime normal, alors que le vrai test n'a que des `PartOffsetting` de 1 à 8 cm).
- **`KSP.log` contient des octets binaires** : `grep -a`, sinon grep annonce « Binary file matches »
  et ne rend rien.
- **Instrumenter plutôt qu'émettre une hypothèse de plus.** Trois hypothèses successives se sont
  révélées fausses ; c'est le log par sondage (`window=… step=… -> N probe(s)`, puis chaque
  `probe at X -> inGround=…`) qui a désigné la cause en une lecture. Le niveau de log se règle dans
  la fenêtre de settings du mod.

## Pièces de test

**Pièce de reproduction du bug `center` : TT-70 Radial Decoupler (`radialDecoupler2`).** Le cas
d'école, pour trois raisons cumulées :

- **un seul collider**, et pas de `MeshCollider` : rien ne peut masquer le défaut dans la boucle
  `foreach (Collider collider in colliders)` ;
- volumes **totalement disjoints** (décalage 0,4455 m contre 0,101 m de demi-épaisseur, 0,243 m de
  trou entre les deux) : jamais détecté, à aucune profondeur ;
- `mass = 0.05 t` → 490 N, sous la limite de 588,4 N : portable par un ingénieur seul **sur Kerbin**.
  `packedVolume = 750` L en revanche, trop gros pour l'inventaire personnel : le saisir sur le
  vaisseau, ou le sortir d'un conteneur.

⚠️ **Mauvaise pièce pour valider la troncature**, en revanche : jusqu'à 0,58 m de son treillis n'a
aucun collider, donc il s'enfonce visuellement de façon parfaitement normale. Pour ça, prendre
l'**Oscar-B vidé de ses ergols**, juste en dessous.

**Oscar-B (`miniFuelTank`)** — la référence pour la traversée du sol :

- **un seul collider**, mesh **convexe**, qui épouse le réservoir visible à 16 mm près : ce qu'on
  voit entrer dans le sol est bien du collider ;
- `rescaleFactor = 1`, et Restock le remplace par `restock-fueltank-0625-4` — même géométrie ;
- `lossyScale = (0.500, 0.187, 0.500)`, **toutes les composantes sous 1** : avant correctif, la boîte
  de recherche était trop petite dans **100 %** des orientations (200 000 tirages), de 53 à 245 mm ;
- ⚠️ **le vider avant de le sortir** : plein, il pèse 0,225 t → **2207 N**, très au-delà de la limite
  de 588,4 N. Vide : 0,025 t = 245 N.

**Basic Fin (`basicFin`)** — *mauvaise* pièce pour ça, malgré les apparences : collider unique,
convexe et fidèle, mais `lossyScale = 20` gonfle la boîte de recherche d'autant. Elle sur-couvre
toujours, donc l'aileron ne s'enfonce **jamais**, quelle que soit l'orientation. Une version cassée
s'y comporte comme une version corrigée.

### Lire un résultat de test

- **Les surfaces ne coïncident pas.** Le terrain de collision (maillage du quad PQS), la hauteur
  analytique PQS et la surface **rendue** sont trois choses différentes. Parallax déplace le rendu
  dans le *vertex shader* et n'écrit **jamais** de `MeshCollider` : sur Kerbin,
  `_DisplacementScale = 0.30` avec `_DisplacementOffset = -0.5`, soit de l'ordre de **±15 cm** entre
  ce qu'on voit et ce sur quoi la physique travaille. **Ne jamais juger une profondeur à l'œil.**
- **Le verdict utile est binaire** : la pièce traverse-t-elle et reste-t-elle dessous ? Et
  l'observable propre est la ligne `colliders touch at … -> stopping at …` dans le log.
- **Piège** : sauvegarder/recharger prouve que le collider ne *traverse* pas — un collider
  entièrement sous la peau ne serait pas éjecté non plus. Croiser avec la couverture mesurée.
