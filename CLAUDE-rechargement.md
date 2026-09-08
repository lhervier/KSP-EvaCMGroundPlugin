# « La base monte quand je recharge » — ce n'est pas le mod

Symptôme : on pose une pièce en construction EVA, on sauvegarde, on recharge, et **toute la
structure a bougé verticalement** — typiquement une ancre dont les clous sortent de terre. Répété,
ça donne l'impression d'une base qui décolle un peu plus à chaque segment.

**Ce n'est pas le mod.** Établi le 2026-09-03, confirmé et surtout **re-expliqué** le 2026-09-08 :
c'est un défaut de KSP, reproductible sans toucher au sol.

⚠️ La cause écrite ici jusqu'au 2026-09-08 (masse gonflée → centre de masse → altitude enregistrée)
était **fausse**. Elle est réfutée plus bas, code et mesures à l'appui. Le mécanisme réel n'a rien à
voir avec la masse.

## La cause : KSP repose le vaisseau au sol à chaque chargement

Au passage hors rails d'un vaisseau posé, [`Vessel.GoOffRails`](file:///d:/ksp-decompiled/Vessel.cs#L5624)
décide s'il faut le recaler sur le terrain :

```csharp
skipGroundPositioning = Landed && !Splashed && situation != PRELAUNCH
                     && PQSminLevel != 0 && PQSmaxLevel != 0
                     && PQSminLevel == mainBody.pqsController.minLevel
                     && PQSmaxLevel == mainBody.pqsController.maxLevel;
...
if (!vesselSpawning && skipGroundPositioning) goto fin;   // on ne touche à rien
if (!CheckGroundCollision()) { ... }
```

[`CheckGroundCollision`](file:///d:/ksp-decompiled/Vessel.cs#L6043) écarte le vaisseau de 5 km, tire
un rayon vers le sol, mesure le point le plus bas de tous ses colliders
([`getLowestPoint`](file:///d:/ksp-decompiled/Vessel.cs#L6266)) et le **repose pile sur le terrain**.
Pour une ancre, dont les clous sont *par construction* dans le sol, ça veut dire : clous dehors.

Et voici le détail qui rend l'ancre si sensible — [`Vessel.cs:6194`](file:///d:/ksp-decompiled/Vessel.cs#L6194) :

```csharp
ModuleGroundPart moduleGroundPart = rootPart.FindModuleImplementing<ModuleGroundPart>();
if (Math.Abs(num3) < 0.1)
{
    if (moduleGroundPart == null) { SetPosition(vector3d); goto fin; }  // < 10 cm : on ignore
}
SetPosition(vector3d + up * num3);                                     // ancre : on applique
```

**La zone morte de 10 cm qui absorbe la correction sur n'importe quel autre vaisseau est
explicitement désactivée quand la pièce racine porte un `ModuleGroundPart`.** Une base ancrée
encaisse donc des recalages millimétriques que tout le reste de la flotte jette.

### Vérifié en jeu

`KSP.log` de la partie « claude », 2026-09-08 — la ligne à chercher est `Moving Vessel` :

```
[LOG 15:34:05.221] [Point d'ancrage Coll-O-Tron]: ground contact! - error. Moving Vessel  down -0.040m
[LOG 15:34:05.263] [ModuleCargoPart]: Part Point d'ancrage Coll-O-Tron velocity 0.0365882 riveting to the ground.
[LOG 16:26:47.573] [Point d'ancrage Coll-O-Tron]: ground contact! - error. Moving Vessel  up 0.001m
[LOG 16:26:47.596] [ModuleCargoPart]: Part Point d'ancrage Coll-O-Tron velocity 0.0365882 riveting to the ground.
```

Trois choses s'y lisent :

- **4 cm et 1 mm** — deux valeurs sous la zone morte : sans le cas particulier `ModuleGroundPart`
  ci-dessus, aucune des deux n'aurait été appliquée ;
- le recalage tombe **20 ms avant le rivetage**, donc l'ancre se **soude au monde à la nouvelle
  position** ([`ModuleGroundPart.MakePartKinematic`](file:///d:/ksp-decompiled/ModuleGroundPart.cs#L813)
  finit par `PermanentGroundContact = true`, `constraints = FreezeAll` et un `FixedJoint` sans
  `connectedBody`). La position corrigée devient l'altitude de référence de la sauvegarde suivante :
  **c'est un cliquet**, pas une oscillation ;
- le signe n'est pas toujours le même (`down` puis `up`) : le recalage n'est pas « toujours vers le
  haut », il vise le contact franc.

Un chargement du même après-midi (15:35:02) produit la ligne de rivetage **sans** ligne
`Moving Vessel` : la passe est bien conditionnelle.

## Pourquoi la condition tombe en défaut

`PQSminLevel` / `PQSmaxLevel` ne sont écrits que par
[`Vessel.checkLanded`](file:///d:/ksp-decompiled/Vessel.cs#L1768), donc **seulement quand le vaisseau
est chargé et testé posé**. Deux façons de se retrouver avec des valeurs qui ne collent pas :

- **une pièce lâchée en construction EVA naît avec 0/0** : `GetProtoVesselNode` écrit en dur
  `PQSMin = 0`, `PQSMax = 0` et `vesselSpawning = true`
  ([`EVAConstructionModeEditor.cs:4149-4153`](file:///d:/ksp-decompiled/EVAConstructionModeEditor.cs#L4149)).
  Tant que le vaisseau n'a pas été rechargé **et** testé posé, la sauvegarde garde 0/0,
  `PQSminLevel != 0` échoue → recalage à chaque chargement ;
- **changer le réglage de détail du terrain change `pqsController.maxLevel`**, donc invalide d'un
  coup les niveaux mémorisés de *tous* les vaisseaux posés → recalage général au prochain
  chargement. (Constaté : un troisième vaisseau à ancre de la même partie porte `PQSMax = 7` là où
  les deux autres portent `10`.)

Mesuré dans la partie « claude », sur le banc de test « ancre + Oscar-B » (`pid 21e94e9e…`), entre la
sauvegarde d'avant manipulation et celle d'après :

| clé | avant | après |
|---|---|---|
| `PQSMin` / `PQSMax` | **0 / 0** | **2 / 10** |
| `hgt` (`heightFromTerrain`) | 64.8565369 — égal à `alt`, donc jamais mesuré | 0.0196 |
| `nrm` (`terrainNormal`) | douteux | normale réelle |
| `alt` | 64.856537635 | 64.856537001 (−0,6 mm) |

Autrement dit : **avant la manipulation, ce vaisseau n'avait jamais été correctement « testé
posé »**, et c'est ça — pas la masse, pas la géométrie — qui armait le recalage au chargement. La
manipulation (ou simplement le fait de charger la scène et de laisser le vaisseau vivre quelques
secondes) remet `PQSMin/PQSMax`, `hgt` et `nrm` d'aplomb.

## Le contournement

**Après avoir posé une pièce, laisser la scène vivre une dizaine de secondes, sauvegarder, puis
recharger une fois et re-sauvegarder.** Le premier rechargement encaisse le recalage et remet
`PQSMin/PQSMax` en phase ; à partir de là `skipGroundPositioning` vaut `true` et KSP ne touche plus
au vaisseau.

Et **ne pas changer le réglage de détail du terrain** avec des bases ancrées en jeu.

⚠️ L'ancien contournement — « attendre 10 à 15 s avant de sauvegarder » — était bien confirmé en jeu,
mais son explication (la fenêtre de masse gonflée) est fausse. L'attente marche probablement parce
qu'elle laisse `checkLanded` tourner, donc renseigner `PQSMin/PQSMax`. Le garder, en sachant que ce
n'est pas la masse qu'on attend.

## La fausse piste principale : la masse et le centre de masse

L'hypothèse était : [`ModuleCargoPart.MakePartSettle`](file:///d:/ksp-decompiled/ModuleCargoPart.cs#L499)
gonfle la masse d'une pièce qu'on vient de reposer, le centre de masse du vaisseau se recentre sur
elle, `Vessel.altitude` est mesurée au centre de masse, donc la sauvegarde enregistre une altitude
aberrante. **Chaque maillon sauf le premier est faux.**

Ce que fait réellement `MakePartSettle` (à connaître, c'est vrai et utile ailleurs) :

```csharp
if (vessel.mainBody.GeeASL < 0.8)       part.prefabMass = 20f;   // Mun, Minmus…
else if (vessel.mainBody.GeeASL < 1.1)  part.prefabMass =  4f;   // Kerbin
// au-delà de 1.1 g (Eve…) : aucun gonflage
```

- **20 t seulement sous 0,8 g. Sur Kerbin c'est 4 t.** (La version précédente de cette note avait la
  condition à l'envers.)
- Seul `prefabMass` est écrit, mais [`Part.FixedUpdate`](file:///d:/ksp-decompiled/Part.cs#L10495)
  appelle `UpdateMass()` à chaque frame physique pour toute pièce non `ACTIVE`, donc `mass` puis
  `rb.mass` suivent en une frame : la masse gonflée atteint bien PhysX.
- **`part.mass` est persisté** ([`ProtoPartSnapshot`](file:///d:/ksp-decompiled/ProtoPartSnapshot.cs#L192),
  écrit ligne 2408, relu 2684). Une sauvegarde tombée dans la fenêtre écrit donc `mass = 4` ou
  `mass = 20` dans le `.sfs` — c'est le marqueur à chercher pour dater la fenêtre. Inoffensif au
  rechargement (`UpdateMass` recalcule depuis `prefabMass`, qui n'est pas persisté).

Mais **l'altitude enregistrée n'a rien à voir avec le centre de masse** :

- `vessel.altitude` n'est écrite que par `mainBody.GetLatLonAlt(vesselTransform.position, …)`
  ([`UpdatePosVel`](file:///d:/ksp-decompiled/Vessel.cs#L7135)), et `vesselTransform = base.transform`
  ([`Vessel.cs:3258`](file:///d:/ksp-decompiled/Vessel.cs#L3258)) alors que le composant `Vessel` est
  posé **sur le GameObject de la pièce racine** ([`Part.cs:5864`](file:///d:/ksp-decompiled/Part.cs#L5864)).
  L'altitude sauvegardée est celle de **la pièce racine**, au centimètre près.
- Au rechargement, [`ProtoVessel`](file:///d:/ksp-decompiled/ProtoVessel.cs#L1186) remet
  `transform.position = GetWorldSurfacePosition(lat, lon, alt)` et chaque autre pièce à
  `position + rot * orgPos`. L'aller-retour est exact.
- Le `CoM` sauvegardé (`vessel.localCoM`) ne sert qu'à calculer `CoMD` tant que le vaisseau est
  *packed* ([`VesselPrecalculate.cs:1347`](file:///d:/ksp-decompiled/VesselPrecalculate.cs#L1347)).
  Il ne positionne rien.

**Réfutation par la mesure**, sur l'autre vaisseau à ancre de la même partie (`pid bf7e851e…`,
ancre + TT-70), entre les deux mêmes sauvegardes : `CoM` passe de `(0.00036, 0.011, -0.0063)` à
`(-0.0074, 0.225, -0.130)` — **22 cm de déplacement du centre de masse enregistré** — pendant que
`alt` reste **bit-à-bit identique** (`64.860710432520136`). Si le centre de masse pilotait l'altitude
enregistrée, c'est là qu'on l'aurait vu.

(Au passage, le `CoM` à `(0,0,0)` du banc Oscar-B après manipulation n'est pas un bug d'altitude mais
la signature de `part.rb == null` pendant `PartOffsetting` — cf. le `CLAUDE.md` parent : une pièce
sans rigidbody est simplement ignorée dans le calcul du centre de masse.)

## L'autre jonglage de masse, à ne pas confondre

[`ModuleGroundPart.MakePartKinematic`](file:///d:/ksp-decompiled/ModuleGroundPart.cs#L813) tourne à
**chaque dépaquetage** d'une ancre déployée. Quand l'ancre est la racine du vaisseau, il met **toutes
les autres pièces** à `prefabMass = mass = rb.mass = 0.01` le temps du rivetage, puis restaure. Deux
détails :

- le restore ligne 1172 remet bien `prefabMass` mais réécrit `mass = 0.01f` et `rb.mass = 0.01f` au
  lieu de les restaurer — copier-coller raté, mais **sans effet** : `UpdateMass()` recorrige à la
  frame suivante. Ne pas partir dessus ;
- `groundAnchor.cfg` déclare **`kinematicDelay = 0.0`**, donc `MakePartSettle` ne temporise pas,
  l'attente d'immobilité (`settleDelay = kinematicDelay * 10`) sort en une frame, et le garde-fou
  `if (!part.GroundContact)` n'est pas armé (il ne l'est que si `kinematicDelay >= 1`). D'où le
  rivetage à **0,0366 m/s** visible dans le log, alors que `placementMaxRivotVelocity` vaut 0,003 :
  **l'ancre se soude où elle se trouve, sans vérifier qu'elle touche le sol.**

## Comment mesurer, la prochaine fois

Deux gestes, et on n'a plus besoin de deviner :

1. **Diff de sauvegardes.** Quicksave juste avant, quicksave juste après, puis comparer le nœud
   `VESSEL` de l'ancre. Ce qu'il faut regarder, dans cet ordre : `PQSMin`/`PQSMax` (0, ou décalés du
   `pqsController` courant = recalage armé), `alt`, `hgt`, `nrm`, puis `position` et `mass` de chaque
   `PART`. Un parseur de `.sfs` tient en vingt lignes de Python (imbrication `nom` / `{` / `}`, un
   `=` par valeur) — beaucoup plus lisible qu'un `diff` brut, où le bruit thermique et orbital noie
   les deux lignes qui comptent.
2. **Filtrer `KSP.log`** sur `Moving Vessel`, `riveting to the ground`,
   `cannot deploy unless on the ground` et `waiting for ground contact`. La ligne `Moving Vessel`
   donne directement l'amplitude et le signe du recalage : si elle est là, l'enquête est finie.

## Fausses pistes — mesurées et écartées

- **La masse gonflée et le centre de masse.** Cf. ci-dessus. Réfutée par le code (l'altitude est
  celle de la pièce racine) *et* par la mesure (22 cm de `CoM`, 0 cm d'`alt`).
- **Le clamp d'altitude au chargement.** `Vessel.Load` remonte un vaisseau posé à la hauteur PQS
  s'il est en dessous ([`Vessel.cs:4054`](file:///d:/ksp-decompiled/Vessel.cs#L4054) →
  [`getCorrectedLandedAltitude`](file:///d:/ksp-decompiled/Vessel.cs#L6785), un `Math.Max` qui ne
  redescend jamais). **Mesuré : `lifted by 0.000 m` sur les dix vaisseaux de la scène.** Il ne se
  déclenche pas. C'est `CheckGroundCollision`, plus loin dans `GoOffRails`, qui agit.
- **Une ancre enfoncée que la physique extrait.** L'intuition était bonne, la mesure était fausse :
  le point bas des colliders avait été comparé à `TerrainAltitude`, c'est-à-dire au champ de hauteur
  **analytique PQS** et non au maillage de collision, et l'écart entre ces deux surfaces (plusieurs
  décimètres, cf. le `CLAUDE.md` parent) avait été pris pour un enfoncement de 37 cm. Ce n'est pas la
  dépénétration PhysX qui extrait l'ancre, c'est `CheckGroundCollision` qui la repose.
- **Une garde au sol insuffisante.** Testée au maximum (10 cm, visible dans les logs) : l'ancre monte
  pareil.

Leçon de méthode : quatre hypothèses solides, chacune appuyée sur du code décompilé, et **les quatre
fausses** — dont une qui est restée écrite ici cinq jours en se présentant comme « solide mais non
mesurée ». Ce qui a tranché, chaque fois, c'est une mesure : le diff de deux `.sfs` et une ligne de
log, pas le raisonnement.
