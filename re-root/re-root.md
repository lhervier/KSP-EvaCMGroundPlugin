# Re-root en vol via édition de la sauvegarde — notes de conception

> Document de référence pour un futur mod KSP « Set as root » (clic droit sur une
> pièce → en faire la racine du vaisseau), destiné à compléter un mod existant qui
> corrige des comportements de la **construction en EVA**.
>
> Tout ici a été **vérifié empiriquement** (sauvegardes réelles, KSP 1.12.5) et/ou
> **confirmé dans la source décompilée** (`D:\ksp-decompiled`, stable).
> En cas de désaccord entre code décompilé et comportement observé : **l'observation fait foi**.

---

## 1. Le problème

En **construction en EVA**, un Kerbal ne peut pas détacher la **pièce racine** d'un
vaisseau (comme dans l'éditeur). Or après un atterrissage « créatif », la pièce qu'on
veut retirer (ex. un port de docking moche/lourd, ou une cabine en haut) se trouve
être la racine → impossible à enlever.

Dans l'éditeur, le re-root est trivial (l'arbre n'est qu'une structure de données).
En vol, deux blocages : la physique est chargée, et on ne peut pas « saisir » la racine
(elle n'a pas de parent). L'**API ne permet pas de re-rooter proprement en vol**.

**Idée du mod** : clic droit → « Set as root » → **sauvegarder** la partie → **patcher le
`.sfs`** → **recharger**. (Même esprit qu'un « switch to vessel ».)

NB : le re-root n'est PAS une opération physique. La physique repose sur des `PartJoint`
entre pièces adjacentes, pas sur « qui est la racine ». La racine n'est qu'un point de
référence (transform, contrôle, persistance). Re-rooter = réécrire des pointeurs.

---

## 2. Concepts KSP utiles

- Un vaisseau = un **arbre de pièces** avec une seule **racine** (la seule sans parent).
- **Attache par nœud** (`attm = 0`) : deux pièces emboîtées par des nœuds nommés
  (`top`, `bottom`…). **Bidirectionnel** : les deux pièces se citent via `attN`.
- **Attache de surface** (`attm = 1`) : une pièce **collée sur la surface** d'une autre.
  Stocké **uniquement sur l'enfant** via `srfN` (l'enfant pointe le collider du parent).
  Le parent n'enregistre rien. → **« A collé sur B » n'a pas de symétrique.**
- **Symétrie** (`sym`) : groupe d'exemplaires symétriques (radial/miroir).

### Découplage fondamental (clé de voûte)

KSP sépare **deux graphes** :

- **Arbre LOGIQUE** = `root` + `parent`. C'est le **seul** que le re-root modifie.
- **Graphe PHYSIQUE** = `attm` / `srfN` / `attN`. Décrit *qui est emboîté/collé sur qui*.
  **Immuable** sous re-root.

Conséquence majeure : un port de docking devenu **racine** garde
`attm=1, srfN=srfAttach,<tank>` (« collé sur le tank ») alors que le tank est désormais
son **enfant**. Les deux cohabitent sans problème. → **Le « problème d'inversion d'attache
de surface » n'existe pas** : rien à convertir, rien à deviner sur les colliders.

---

## 3. Format `.sfs` (vaisseau en vol)

Structure : `GAME { FLIGHTSTATE { VESSEL { PART {…} PART {…} … } } }`.
Les pièces se référencent **par index positionnel** dans la liste des `PART`.

### Niveau VESSEL (champs pertinents)

```
root = 0                 # index de la pièce racine (la racine a aussi parent = son propre index)
ref  = 3195625251        # uid de la pièce de contrôle (PAS la racine ; ne pas confondre)
rot  = x,y,z,w           # orientation MONDE de la racine (repère surface, pour vaisseau posé)
CoM  = x,y,z             # centre de masse, relatif à la racine
lat / lon / alt / hgt / nrm   # placement au sol de la racine
```

### Niveau PART (champs pertinents)

```
cid = 4293855334                 # craft id (PARTAGÉ entre instances du même engin → PAS unique)
uid = 131499844                  # unique par instance de pièce
parent = 0                       # index du parent (la racine pointe sur elle-même)
position = x,y,z                 # orgPos : position RELATIVE à la racine
rotation = x,y,z,w               # orgRot : orientation RELATIVE à la racine
attm = 1                         # 0 = attache par nœud, 1 = attache de surface
sym  = 5                         # index d'un exemplaire symétrique (peut apparaître plusieurs fois)
srfN = srfAttach, 0,COL1         # attache de surface vers la pièce d'index 0, collider "COL1"
                                 #   (ou "srfN = , -1" / "srfN = None, -1" si pas d'attache surface)
attN = top, -1                   # nœud "top" libre
attN = bottom, 2                 # nœud "bottom" relié à la pièce d'index 2  (bidirectionnel)
```

`position`/`rotation` sont **relatifs à la racine** (la racine est donc toujours à
`0,0,0` / `0,0,0,1`). **Changer de racine impose de tout réexprimer** dans le repère de
la nouvelle racine.

### Exemple concret (vaisseau « reservoir », racine = fuelTank)

```
root = 0
# index 0 — fuelTankSmallFlat (RACINE)
parent = 0 ; position = 0,0,0 ; rotation = 0,0,0,1 ; attm = 0
srfN = srfAttach, -1 ; attN = top, -1 ; attN = bottom, 2
# index 1 — dockingPort3 (collé en surface sur le tank)
parent = 0 ; attm = 1 ; srfN = srfAttach, 0,COL1
# index 2 — probeCoreOcto2 (emboîté par nœud sur le tank)
parent = 0 ; attm = 0 ; attN = bottom, 3 ; attN = top, 0
# index 3 — longAntenna (sur le probe)
parent = 2 ; attN = bottom, 2
# index 4/5 — batteryPack ×2 (collées, symétriques)
parent = 0 ; attm = 1 ; sym = 5 ; srfN = srfAttach, 0,COL1
parent = 0 ; attm = 1 ; sym = 4 ; srfN = srfAttach, 0,COL1
```

---

## 4. Format `.craft` (éditeur) — pour info

Différent du `.sfs` :

- Arbre encodé par `link` : **le parent liste ses enfants** (`link = <nom>_<craftID>`).
- **Racine = la pièce vers laquelle personne ne pointe** (PAS l'ordre du fichier ;
  l'éditeur ne réordonne pas). `srfN`/`attN` portent en plus la géométrie d'ancrage.
- **Pas de champ `root` explicite** (déduit des links).

Important : l'éditeur **tolère un ordre quelconque** (il reconstruit l'arbre depuis les
links). **Ce n'est PAS le cas du `.sfs` en vol** (voir §6, piège du réordonnancement).

---

## 5. La transformation de re-root (spec validée)

Re-rooter le `.sfs` d'un vaisseau en vol vers une nouvelle racine `N` :

1. **Arbre** : `root = N` ; inverser les `parent` du **seul chemin** entre la nouvelle et
   l'ancienne racine (la nouvelle devient auto-référente ; tout ce qui est hors du chemin
   garde son parent, par identité de pièce).
2. **Géométrie des pièces** : réexprimer `position`/`rotation` (orgPos/orgRot) de chaque
   pièce dans le repère de la nouvelle racine.
3. **Placement du vaisseau** : recalculer `rot` (orientation monde de la nouvelle racine),
   `CoM`, et `lat`/`lon`/`alt` (position de la nouvelle racine). **Indispensable** (voir §6).
4. **Attaches** : **PRÉSERVER** `attm`/`srfN`/`attN` tels quels (la pièce d'origine reste
   le « collé »). Ne rien inverser.
5. **Symétrie** : purger les groupes devenus invalides — règle exacte : un groupe n'est
   valide que si **tous ses exemplaires sont à la même profondeur de hiérarchie**.
6. **Ordre** : **réordonner** les `PART` en **ordre topologique** (racine en tête, parent
   avant enfant) **ET renuméroter toutes les références d'index** (`root`, `parent`,
   `attN`, `srfN`, `sym`). ← l'étape critique (voir §6).

---

## 6. Découvertes & pièges (chacun vérifié)

### 6.1 Réordonnancement OBLIGATOIRE (sinon KRAKEN)

L'édition « en place » (changer `root` + `parent` + géométrie **sans réordonner**) **plante**.
Test mené : re-root de « dockingport » vers le fuelTank en laissant l'ordre
`[dockingPort3(0), fuelTank(1), …]`. Résultat en jeu : **« génération spontanée de milliers
de ports de docking »** (explosion/duplication). Cause : un **enfant** (dockingPort3, idx 0)
précède son **parent** (fuelTank, idx 1) ; au chargement en vol KSP résout les `parent` par
index **séquentiellement**, l'arbre est malformé, le `ModuleDockingNode` part en boucle.

➡️ La liste `.sfs` **doit** être en ordre topologique + indices renumérotés. **Non négociable.**
(C'est exactement ce que fait le **lancement** : il réordonne racine-en-tête.)

### 6.2 Placement au sol (déplacement / pseudo-rotation)

Si on garde `lat`/`lon`/`alt` d'origine après changement de racine, le vaisseau est **translaté**
de l'offset (ancienne→nouvelle racine) et **semble avoir tourné** (l'ancrage + l'orientation de
référence changent ensemble). La gravité rattrape sur un petit vaisseau, mais c'est
**catastrophique sur un gros** (clipping terrain + explosion). → recaler `lat/lon/alt`/`rot`
sur la nouvelle racine (trivial via l'API, voir §8).

### 6.3 Attache de surface préservée (pas d'inversion)

Confirmé par 3 sources concordantes (reservoir.craft, dockingport.craft, et les `.sfs`) :
le re-root **ne touche pas** `attm`/`srfN`. La pièce d'origine reste « collée », et devient
juste parent dans l'arbre. (Voir §7 pour la nuance code/observation.)

### 6.4 Symétrie

`StripInvalidSymmetrySets` : on purge un groupe de symétrie dès qu'un exemplaire n'est plus
à la même profondeur de hiérarchie que les autres. Une racine ne peut pas rester membre d'un
groupe radial.

---

## 7. Référence : code décompilé (`D:\ksp-decompiled`, stable)

> Le décompilateur injecte du bruit (`while(true) switch(N){case 0: continue}` + `goto`) —
> à ignorer. Numéros de ligne valables pour la version présente dans `D:\ksp-decompiled`.

- **`EditorLogic.cs:7371`** → appelle `EditorReRootUtil.MakeRoot(newRoot, selectedPart)`
  (point d'entrée du re-root éditeur). Candidats : `GetRootCandidates` (`EditorLogic.cs:4392`).
- **`EditorReRootUtil.cs`**
  - `MakeRoot` (l.205) : valide (`isRootCandidate`, `HierarchyUtil.isDescendant`) puis
    `newRoot.SetHierarchyRoot(newRoot)` (l.239) puis `StripInvalidSymmetrySets` (l.240).
  - `StripInvalidSymmetrySets` (l.244) / `SymmetrySetIsValid` (l.299, via `GetHierarchyDepth`).
- **`Part.cs`**
  - `SetHierarchyRoot(root)` (l.6964) : **inversion RÉCURSIVE de la chaîne `parent`** (chaque
    pièce prend son ex-enfant comme parent, transforme son ex-parent en enfant, récurse).
    Branche vol : `physicalSignificance==FULL && !LoadedSceneIsEditor` → `DestroyJoint` +
    `CreateAttachJoint` + `ResetJoints`.
  - `UpdateOrgPosAndRot(newRoot)` (l.7100) :
    `orgPos = newRoot.partTransform.InverseTransformPoint(partTransform.position)` ;
    `orgRot = Quaternion.Inverse(newRoot.partTransform.rotation) * partTransform.rotation`.
    → **la formule exacte du reframe.**
- **`AttachNode.cs`** : `ReverseSrfNodeDirection` (l.145), `ChangeSrfNodePosition` (l.183).
- **`ProtoPartSnapshot.cs`** (sérialisation `.sfs`) : `attm` = `attachMode` (l.~2320) ;
  `srfN` = `srfAttachNode.Save()` (l.~2376) ; lecture `srfN`/`attm` (l.~463 / ~650).
- **`ShipConstruct.cs`** (sérialisation `.craft`) : `link` = enfants (l.299) ; `srfN` écrit
  **seulement si** `srfAttachNode.attachedPart != null` (l.320), valeur l.355.

### ⚠️ Nuance non résolue (attache de surface)

Lu littéralement, `SetHierarchyRoot` (bloc `flag`, l.~7065-7088) + `ReverseSrfNodeDirection`
font que **l'ex-parent** devient le surface-attaché (`attm=SRF_ATTACH`, son nœud pointe la
nouvelle racine). **Mais le `.craft` ET le `.sfs` réels (même 1.12.5) montrent l'INVERSE** :
la pièce d'origine reste « collée ». Cause probable : bruit du décompilateur masquant une
inversion de branche. **→ Sur ce point précis, ne pas se fier au code : suivre l'observation
(préserver `attm`/`srfN`).** Les autres mécaniques (inversion récursive, `UpdateOrgPosAndRot`,
règle de symétrie) sont, elles, **confirmées par l'observation**.

---

## 8. Implémentation du mod (le chemin facile via l'API KSP)

Gros avantage : le mod tourne **en jeu**, il lit les `Transform` **réels**. Il n'a donc
**presque rien à calculer** — il ré-exprime l'état physique courant par rapport à la nouvelle
racine. Toute la difficulté de l'édition manuelle (emprunter une géométrie, quaternions à la
main, conversion cartésien→géodésique) **disparaît**.

Pour chaque donnée à écrire dans le `.sfs` après `SaveGame` :

- **Placement vaisseau** (corrige §6.2) :
  - `lat = mainBody.GetLatitude(newRoot.transform.position)`
  - `lon = mainBody.GetLongitude(newRoot.transform.position)`
  - `alt = mainBody.GetAltitude(newRoot.transform.position)`
  - `rot` = orientation monde de `newRoot` (ramenée au repère surface)
- **Géométrie des pièces** : pour chaque pièce,
  - `position = newRoot.transform.InverseTransformPoint(part.transform.position)`
  - `rotation = Quaternion.Inverse(newRoot.transform.rotation) * part.transform.rotation`
- **Arbre** : `root` = index de `newRoot` ; recalculer les `parent` (chemin inversé).
- **Attaches** : recopier `attm`/`srfN`/`attN` inchangés.
- **Symétrie** : appliquer la règle même-profondeur.
- **Ordre** : émettre les `PART` en parcours depuis la racine (parent avant enfant) et
  renuméroter toutes les références d'index.

### Orchestration

1. Clic droit sur une pièce → action « Set as root » (PartModule / event).
2. `GamePersistence.SaveGame(...)` (ou récupérer le `ConfigNode` courant).
3. Charger le `ConfigNode`, retrouver le `VESSEL` ciblé, appliquer la transformation §5.
4. Réécrire, puis recharger (`GamePersistence.LoadGame(...)` / re-focus du vaisseau).

> Alternative à étudier : `Part.SetHierarchyRoot` possède une **branche vol**
> (`!LoadedSceneIsEditor`). Tenter le re-root en mémoire **sans** passer par le disque est
> possible mais risqué (stabilité physique) ; l'approche save→patch→reload est plus sûre.

---

## 9. Fichiers d'exemple / fixtures (⚠️ à conserver si tu veux re-tester)

Les `.sfs`/`.craft` d'exemple **ne sont pas conservés** par défaut. Je les ai regroupés dans
**`temp/re-root/`** (⚠️ `temp/` peut être nettoyé — **déplace ce dossier ailleurs si tu veux
les garder**). Contenu :

- `fixtures/reservoir.craft` + `fixtures/dockingport.craft` : **même engin**, avant/après
  re-root **dans l'éditeur** (prouve §6.3 + format `.craft`). dockingport = port de docking
  passé racine.
- `fixtures/sauvegarde rapide.sfs` : les deux vaisseaux **lancés** (`reservoir` racine=tank,
  `dockingport` racine=port). Oracle structurel idéal (même engin, deux racines).
- `fixtures/sauvegarde rapide #5.sfs` : **entrée du test** (les 2 vaisseaux posés côte à côte
  + l'ingénieur Bill).
- `fixtures/sauvegarde rapide #5 - reroot.sfs` : **sortie prouvée** — `dockingport` re-rooté
  vers le fuelTank (en place + réordonné). Recharge OK, Bill peut détacher le port.
- `scripts/reroot-edit.js` : applique root+parent+reframe (édition ligne à ligne, assertions).
- `scripts/reroot-reorder.js` : réordonne les `PART` (topologique) + renumérote les indices.

> Oracle clé : « reservoir » et « dockingport » partagent les mêmes `cid` (même engin), donc
> les positions relatives à une racine donnée sont identiques entre eux → patron parfait.

La source décompilée reste dans **`D:\ksp-decompiled`** (stable, ne bouge pas).

---

## 10. État & TODO

- ✅ Mécanique de re-root comprise, prouvée en jeu, et spec figée (§5).
- ✅ Pièges identifiés et résolus : réordonnancement obligatoire (§6.1), placement (§6.2).
- ✅ Formules API pour une implémentation propre (§8).
- ⬜ Écrire le **patcheur `.sfs`** (parseur `ConfigNode` + les 6 étapes), testable hors-jeu
  sur les fixtures (`#5.sfs` → doit reproduire `#5 - reroot.sfs`, modulo réordonnancement
  identique).
- ⬜ Brancher au jeu : action « Set as root » (clic droit) + orchestration save→patch→reload,
  en lisant les `Transform` réels (§8). À intégrer dans le mod EVA-construction existant.
- ⬜ Garde-fous : refuser si la nouvelle racine n'est pas `allowRoot` ; gérer les
  `ModuleDockingNode` en état `Docked`/`PreAttached` (références `dockUId` par uid, stables) ;
  prévenir/valider sur gros vaisseaux.
