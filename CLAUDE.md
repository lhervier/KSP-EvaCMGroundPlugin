# KSP-EvaCMGroundPlugin — notes de développement

Ce fichier ne contient que ce qui est **propre à ce mod**. Tout ce qui vaut pour le développement de
mods KSP en général est dans le `CLAUDE.md` du dossier parent (`kspmod\`) — en particulier, et parce
que ce mod repose entièrement dessus :

- **le sol et les bâtiments sont des peaux sans épaisseur**, donc `ComputePenetration` ne détecte que
  pendant le chevauchement, et un test sur une seule pose peut être enjambé ;
- **`Physics.Overlap*` et `Physics.ComputePenetration` n'attendent pas le même point** ;
- **`Physics.autoSyncTransforms` est désactivé** dans KSP ;
- **construction EVA** : la limite de portage est un poids, et l'écart entre la pose demandée et la
  pose accordée grandit tant que le joueur maintient le glissement ;
- **le collider d'une pièce ne couvre pas forcément sa forme** ;
- **lire un `.mu` depuis un script** pour vérifier tout ça sans lancer le jeu.

## Fichiers de contexte — à ouvrir au besoin, pas par défaut

Le détail des enquêtes est sorti d'ici pour ne pas alourdir la lecture courante :

- **[CLAUDE-detection-sol.md](CLAUDE-detection-sol.md)** — comment le mod décide qu'une pièce est
  dans le sol : les trois étages (cast, sondages, dichotomie), les quatre invariants à ne pas casser,
  l'angle mort des colliders concaves, les impasses à ne pas refaire (`Rigidbody.SweepTest`,
  `lossyScale`), les pièces de test et la méthode de lecture des logs. **À lire avant de toucher à
  `GetReachablePosition`, `GetCastDistance`, `IsPoseInGround` ou au balayage.**
- **[CLAUDE-rechargement.md](CLAUDE-rechargement.md)** — « la base monte quand je recharge » :
  **ce n'est pas le mod**, c'est `Vessel.CheckGroundCollision` qui repose le vaisseau « point le plus
  bas des colliders sur le terrain » à chaque chargement, parce que `PQSMin`/`PQSMax` de la
  sauvegarde ne correspondent pas au `pqsController` courant — et parce que la zone morte de 10 cm
  qui absorberait la correction est désactivée quand la racine porte un `ModuleGroundPart`.
  Contournement, la méthode de mesure (diff de `.sfs`, ligne `Moving Vessel` du log), et les quatre
  fausses pistes — **dont la masse de stabilisation de `ModuleCargoPart`, qui était l'explication
  écrite ici jusqu'au 2026-09-08 et qui est réfutée**. **À lire dès que quelqu'un impute au mod un
  déplacement constaté au rechargement.**

## Ce que fait le mod, en une phrase

Pendant le mode construction EVA, il écoute `GameEvents.onEditorPartEvent` et **tronque** le
déplacement demandé : la pièce avance jusqu'au contact réel du sol, moins la garde au sol réglable.
Tout est dans [`Src/EvaCMGroundMod.cs`](Src/EvaCMGroundMod.cs).

⚠️ Ce n'était pas le cas avant le 2026-09-03 : le mod **refusait** le déplacement et reposait la
pièce à sa dernière pose valide. La troncature remplace ce fonctionnement, parce qu'un refus laisse
la pièce n'importe où au-dessus du sol, et que l'erreur s'accumule sur une base longue — les
contraintes se paient au démarrage de la physique.

## Le second correctif, sans rapport avec le premier

Depuis le 2026-09-08, le mod embarque aussi
[`Src/anchor/AnchoredBaseGroundKeeper.cs`](Src/anchor/AnchoredBaseGroundKeeper.cs), qui empêche KSP
de **reposer une base ancrée sur le terrain à chaque chargement** — le bug documenté dans
[CLAUDE-rechargement.md](CLAUDE-rechargement.md). Il écrit `PQSminLevel`/`PQSmaxLevel` dans le
`ProtoVessel`, sur `GameEvents.onProtoVesselLoad`, ce qui remet le vaisseau sur le chemin « rien à
faire » de `Vessel.GoOffRails`. **Activé par défaut** depuis le 2026-09-08 — il l'était à
`false` le temps de l'éprouver en jeu — via le réglage `keepAnchoredBaseGroundPosition`.

Il n'a **aucun lien** avec la troncature de placement, et c'est délibéré : son propre addon, son
propre réglage, aucun état partagé. Il mériterait un mod à part et pourra y être déplacé tel quel.
Quatre choix à ne pas défaire sans relire le fichier de contexte :

- **`onProtoVesselLoad`, pas `onVesselLoaded`** : un vaisseau ancré court-circuite les 75 frames de
  *physics hold*, donc il se dépaquette immédiatement. Le hook doit être antérieur à l'existence du
  `Vessel`, sinon il y a une course.
- **Le tir depuis le constructeur `ProtoVessel(ConfigNode, Game)` est ignoré** (`action.to != null`) :
  à ce moment rien n'est encore parsé.
- **On ne touche pas à `vesselSpawning`** : c'est lui qui laisse une ancre qu'on vient de lâcher
  recevoir son assise initiale.
- **Variante conservatrice assumée** : on ne corrige que des niveaux à `0/0` (jamais initialisés, cas
  de la pièce lâchée). Des niveaux simplement périmés veulent dire que le détail du terrain a
  vraiment changé, et là la passe de KSP a une raison d'être.

## Deux variables à ne jamais refusionner

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

## `Collider.bounds` est déjà en espace monde

Corrigé le 2026-09-03. `GetMeshColliders` bâtit son volume de recherche sur `meshCollider.bounds` :
ses extents sont **déjà** en unités monde (pas de `lossyScale` à appliquer) et ses axes sont **déjà**
ceux du monde, d'où `Quaternion.identity` en rotation. Remultiplier par `lossyScale` gonflait la
boîte (×20 sur certaines pièces, ×0,5 sur d'autres) et lui appliquer `transform.rotation` permutait
ses dimensions entre axes. Détail et mesures dans [CLAUDE-detection-sol.md](CLAUDE-detection-sol.md).

## La garde au sol est une vraie distance

Depuis la troncature, le paramètre n'est plus un biais sur un test booléen : il est **retranché du
contact exact**, donc la pièce s'immobilise précisément à cette hauteur du sol. À 0 elle se pose au
contact franc — mais une marge strictement positive reste utile, c'est elle qui garantit que la pose
enregistrée est franchement non chevauchante malgré les arrondis. Défaut : 1 cm, plafond 10 cm.

## Reproduire et valider

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
l'**Oscar-B vidé de ses ergols** — les critères et le protocole sont dans
[CLAUDE-detection-sol.md](CLAUDE-detection-sol.md).

**Piège de méthode** : sauvegarder/recharger la partie prouve que le collider ne **traverse** pas le
terrain — un collider entièrement sous la peau ne serait pas éjecté non plus. Le test seul ne
distingue pas les deux cas ; le croiser avec la couverture mesurée du collider.

## Chantiers ouverts

- **Les colliders concaves échappent au prédicat** (7 pièces stock transportables, dont le point
  d'ancrage) : `ComputePenetration` ne rend rien entre deux maillages concaves, et le terrain l'est
  toujours. Piste esquissée, non implémentée — voir
  [CLAUDE-detection-sol.md](CLAUDE-detection-sol.md). Noter que ce n'est **pas** ce qui faisait
  monter les bases, contrairement à ce qu'on a cru un moment.
- **La garde au sol est retranchée le long du déplacement**, pas verticalement — alors que le
  comportement d'origine décalait le volume de test vers le bas (`GetGroundOffsetVector`). Un
  ajustement final presque horizontal donne donc une garde verticale quasi nulle. Ça n'a pas eu de
  conséquence mesurable (la marge à 10 cm ne change rien au symptôme qu'on lui imputait, cf.
  [CLAUDE-rechargement.md](CLAUDE-rechargement.md)), mais l'écart entre le nom du paramètre et ce
  qu'il fait reste à trancher.
