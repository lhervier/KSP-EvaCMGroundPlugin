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

### Les quatre invariants à ne pas casser

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
- **Se fier à `lossyScale` sans le mesurer.** Avant correctif, la phase large multipliait les extents
  de `bounds` par `lossyScale` — alors que `Collider.bounds` est déjà en espace monde. Sur le Basic
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
