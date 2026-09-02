# KSP-EvaCMGroundPlugin — notes de développement

Ce fichier ne contient que ce qui est **propre à ce mod**. Tout ce qui vaut pour le développement de
mods KSP en général est dans le `CLAUDE.md` du dossier parent (`kspmod\`) — en particulier, et parce
que ce mod repose entièrement dessus :

- **le sol et les bâtiments sont des peaux sans épaisseur**, donc `ComputePenetration` ne détecte que
  pendant le chevauchement, et un test sur une seule pose peut être enjambé ;
- **`Physics.Overlap*` et `Physics.ComputePenetration` n'attendent pas le même point** ;
- **`Physics.autoSyncTransforms` est désactivé** dans KSP ;
- **construction EVA** : le gizmo ne recule jamais, et la limite de portage est un poids ;
- **le collider d'une pièce ne couvre pas forcément sa forme** ;
- **lire un `.mu` depuis un script** pour vérifier tout ça sans lancer le jeu.

## Ce que fait le mod, en une phrase

Pendant le mode construction EVA, il écoute `GameEvents.onEditorPartEvent`, teste si la pièce tenue
se retrouve dans le sol, et la repose à sa dernière pose valide (`previousPosition` /
`previousRotation`) le cas échéant. Tout est dans [`Src/EvaCMGroundMod.cs`](Src/EvaCMGroundMod.cs).

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

## Le balayage du trajet, et pourquoi il existe

Corrigé le 2026-09-03. `OnEditorPartEvent` parcourt le trajet **pose par pose** depuis la dernière
pose valide, au lieu de ne tester que l'arrivée.

Sans ça : le gizmo de l'éditeur ne recule pas quand le mod repose la pièce, l'écart entre les deux
grandit tant que le joueur glisse, et un événement finit par déplacer la pièce de plus que
l'épaisseur de son collider — qui passe d'« au-dessus » du terrain à « en dessous » sans jamais être
« dedans ». Symptôme en jeu : *ça bloque, puis si j'insiste, ça passe*, et une fois passée la pièce
est verrouillée sous terre puisque `previousPosition` enregistre la pose acceptée.

- `GetSweepStepCount` — le pas vaut la moitié du collider le plus mince (`GetSmallestThickness`),
  pour qu'au moins une pose intermédiaire tombe *dans* toute surface traversée. Plafond
  `MAX_SWEEP_STEPS = 32`.
- **La rotation fait partie du trajet** : un collider éloigné de l'origine parcourt un arc, qui
  enjambe une surface exactement comme une translation. Le nombre de pas intègre `angle × rayon`, et
  les poses intermédiaires interpolent aussi la rotation (`Quaternion.Slerp`) — sinon les poses
  testées ne seraient pas sur le trajet réel.
- `Physics.SyncTransforms()` avant chaque test de pose, et une fois après la décision.
- Sortie anticipée dès qu'une pose est dans le sol : le cas coûteux (32 pas complets) ne survient
  qu'en vol libre, loin du sol — c'est là qu'un éventuel à-coup se verrait.

Quand la pièce bouge peu entre deux événements, `steps` vaut 1 et le comportement est celui d'avant.

## Reproduire et valider

**Pièce de reproduction du bug `center` : TT-70 Radial Decoupler (`radialDecoupler2`).** Le cas
d'école, pour trois raisons cumulées :

- **un seul collider**, et pas de `MeshCollider` : rien ne peut masquer le défaut dans la boucle
  `foreach (Collider collider in colliders)` ;
- volumes **totalement disjoints** (décalage 0,4455 m contre 0,101 m de demi-épaisseur, 0,243 m de
  trou entre les deux) : jamais détecté, à aucune profondeur ;
- `mass = 0.05 t` → 490 N, sous la limite de 588,4 N : portable par un ingénieur seul **sur Kerbin**,
  pas besoin d'aller sur la Mun. `packedVolume = 750` L en revanche, trop gros pour l'inventaire
  personnel d'un kerbal : le saisir sur le vaisseau, ou le sortir d'un conteneur (MK3 Cargo Storage
  Unit, Hitchhiker).

**Protocole** : ingénieur en EVA, mode construction, saisir le TT-70, l'orienter bras vers le bas et
l'enfoncer. ⚠️ **Garder l'origine de la pièce au-dessus du terrain** (la boule du gizmo) : si elle
passe sous le sol, le pré-test d'altitude déclenche le retour arrière **avant** d'arriver aux
colliders, et une version cassée se comporte comme une version corrigée.

⚠️ **Mauvaise pièce pour valider le balayage, en revanche** : c'est la même propriété — un collider
unique, loin de tout le reste — qui expose si bien le bug `center` et qui rend le TT-70 trompeur
ensuite. Jusqu'à 0,58 m de son treillis n'a aucun collider (mesures dans le `CLAUDE.md` parent), donc
il s'enfonce visuellement de façon parfaitement normale. Pour valider le balayage, prendre une pièce
dont le collider épouse la forme : `winglet`, `FuelCell`, `adapterSmallMiniTall`.

**Piège de méthode** : sauvegarder/recharger la partie prouve que le collider ne **traverse** pas le
terrain — un collider entièrement sous la peau ne serait pas éjecté non plus. Le test seul ne
distingue pas les deux cas ; le croiser avec la couverture mesurée du collider.

## Piste laissée ouverte

Le pré-test d'altitude de `IsPoseInGround` porte toujours sur `part.transform.position` **seul** — un
point unique, qui pour une pièce comme le TT-70 est à 0,41–0,62 m de son unique collider. C'est le
même défaut point-pour-volume que celui corrigé sur `collider.center`, un étage plus haut.

L'étendre au centre des colliders a été **délibérément écarté** le 2026-09-03 : il repose sur
`TerrainAltitude`, c'est-à-dire le champ de hauteur PQS, aveugle aux bâtiments et aux statiques. Un
bâtiment Kerbal Konstructs posé **enfoncé** dans le terrain a son plancher sous l'altitude PQS ; une
pièce posée dessus serait renvoyée en arrière, rendant la construction **impossible à cet endroit**.
Le balayage couvre le même besoin sans ce faux positif, et le pré-test ne sert plus que de garde-fou
grossier.
