# KSP-EvaCMGroundPlugin — notes de développement

Ce fichier ne contient que ce qui est **propre à ce mod**, et seulement ce qui sert à chaque séance :
ce que le mod fait, et où aller chercher le reste. Le détail de chaque enquête est dans les fichiers
de contexte listés plus bas — **une connaissance nouvelle va dans le fichier de son sujet, pas ici**.

Tout ce qui vaut pour le développement de mods KSP en général est dans le `CLAUDE.md` du dossier
parent (`kspmod\`) et ses fichiers de contexte — ici, presque tout vient de
[`../CLAUDE-terrain-physique.md`](../CLAUDE-terrain-physique.md), dont ce mod dépend entièrement :

- **le sol et les bâtiments sont des peaux sans épaisseur**, donc `ComputePenetration` ne détecte que
  pendant le chevauchement, et un test sur une seule pose peut être enjambé ;
- **`Physics.Overlap*` et `Physics.ComputePenetration` n'attendent pas le même point** ;
- **`Physics.autoSyncTransforms` est désactivé** dans KSP ;
- **construction EVA** : la limite de portage est un poids, et l'écart entre la pose demandée et la
  pose accordée grandit tant que le joueur maintient le glissement ;
- **le collider d'une pièce ne couvre pas forcément sa forme** ;
- **lire un `.mu` depuis un script** pour vérifier tout ça sans lancer le jeu
  ([`../CLAUDE-modelisation-mu.md`](../CLAUDE-modelisation-mu.md)).

## Ce que fait le mod, en une phrase

Pendant le mode construction EVA, il écoute `GameEvents.onEditorPartEvent` et **tronque** le
déplacement demandé : la pièce avance jusqu'au contact réel du sol, moins la garde au sol réglable.
Tout est dans [`Src/EvaCMGroundMod.cs`](Src/EvaCMGroundMod.cs) ; le fonctionnement de la détection
elle-même est dans [CLAUDE-detection-sol.md](CLAUDE-detection-sol.md).

⚠️ **Seuls les déplacements au gizmo sont tronqués** (`PartOffsetting`, `PartOffset`, `PartRotating`,
`PartRotated`) — filtre `IsGizmoMove`, ajouté le 2026-09-08. `onEditorPartEvent` porte dix types
d'événements, et les autres sont des placements que KSP a déjà décidés : tronquer un `PartAttached`
poserait la pièce **à côté du nœud** sur lequel ses données d'attachement disent qu'elle est, et
`PartDetached` tire sur `hoveredPart`, donc sur une **autre pièce** que celle qu'on suit. Sur un
événement non filtré on **cesse de suivre la pièce** (`previousPart = null`) plutôt que de conserver
une pose de référence périmée : le prochain déplacement au gizmo repart de là où la pièce est
vraiment.

⚠️ Ce n'était pas le cas avant le 2026-09-03 : le mod **refusait** le déplacement et reposait la
pièce à sa dernière pose valide. La troncature remplace ce fonctionnement, parce qu'un refus laisse
la pièce n'importe où au-dessus du sol, et que l'erreur s'accumule sur une base longue — les
contraintes se paient au démarrage de la physique.

## Le second correctif, sans rapport avec le premier

Depuis le 2026-09-08, le mod embarque aussi
[`Src/anchor/AnchoredBaseGroundKeeper.cs`](Src/anchor/AnchoredBaseGroundKeeper.cs), qui empêche KSP
de **reposer une base ancrée sur le terrain à chaque chargement**. Activé par défaut, via le réglage
`keepAnchoredBaseGroundPosition`.

Il n'a **aucun lien** avec la troncature de placement, et c'est délibéré : son propre addon, son
propre réglage, aucun état partagé. Il mériterait un mod à part et pourra y être déplacé tel quel.
Le bug qu'il corrige, les quatre choix de conception à ne pas défaire et la validation en jeu sont
dans [CLAUDE-rechargement.md](CLAUDE-rechargement.md).

## Fichiers de contexte — à ouvrir au besoin, pas par défaut

Chaque ligne en dit assez pour savoir **si** le fichier concerne la tâche en cours ; ne l'ouvrir que
dans ce cas.

- **[CLAUDE-detection-sol.md](CLAUDE-detection-sol.md)** — comment le mod décide qu'une pièce est
  dans le sol : les trois étages (cast, sondages, dichotomie), les cinq invariants à ne pas casser,
  les **quatre pièges de géométrie déjà payés** (`center` contre `transform.position`,
  `Collider.bounds` déjà en espace monde, les colliders trigger et désactivés, la garde au sol
  comptée une seule fois), l'angle mort des colliders concaves, les impasses à ne pas refaire
  (`Rigidbody.SweepTest`, `lossyScale`), les pièces de test et la méthode de lecture des logs.
  **À lire avant de toucher à `GetReachablePosition`, `GetCastDistance`, `IsPoseInGround`, au
  balayage ou à la garde au sol.**
- **[CLAUDE-rechargement.md](CLAUDE-rechargement.md)** — « la base monte quand je recharge » :
  **ce n'est pas le mod**, c'est `Vessel.CheckGroundCollision` qui repose le vaisseau « point le plus
  bas des colliders sur le terrain » à chaque chargement, parce que `PQSMin`/`PQSMax` de la
  sauvegarde ne correspondent pas au `pqsController` courant — et parce que la zone morte de 10 cm
  qui absorberait la correction est désactivée quand la racine porte un `ModuleGroundPart`. Contient
  aussi le correctif `AnchoredBaseGroundKeeper` (ses quatre choix de conception), le contournement
  sans le mod, la méthode de mesure (diff de `.sfs`, ligne `Moving Vessel` du log) et les quatre
  fausses pistes — **dont la masse de stabilisation de `ModuleCargoPart`, qui était l'explication
  écrite jusqu'au 2026-09-08 et qui est réfutée**. **À lire dès que quelqu'un impute au mod un
  déplacement constaté au rechargement.**

## Chantiers ouverts

- **Les colliders concaves échappent au prédicat** (7 pièces stock transportables, dont le point
  d'ancrage) : `ComputePenetration` ne rend rien entre deux maillages concaves, et le terrain l'est
  toujours. Piste esquissée, non implémentée — voir
  [CLAUDE-detection-sol.md](CLAUDE-detection-sol.md). Noter que ce n'est **pas** ce qui faisait
  monter les bases, contrairement à ce qu'on a cru un moment.
