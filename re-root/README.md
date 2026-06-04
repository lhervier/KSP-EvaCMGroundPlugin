# Bundle re-root (fixtures + scripts)

⚠️ Ce dossier est dans `temp/` qui **peut être nettoyé**. **Déplace-le ailleurs**
(hors `temp/`) si tu veux conserver les données de test du re-root.

- `re-root.md` — copie du doc de conception (canonique : `/re-root.md` à la racine du dépôt).
- `fixtures/` — sauvegardes d'exemple (voir §9 du doc) :
  - `reservoir.craft` / `dockingport.craft` — même engin, avant/après re-root éditeur.
  - `sauvegarde rapide.sfs` — les 2 vaisseaux lancés (oracle structurel).
  - `sauvegarde rapide #5.sfs` — entrée du test (2 vaisseaux + Bill l'ingénieur).
  - `sauvegarde rapide #5 - reroot.sfs` — sortie prouvée (recharge OK, port détachable).
- `scripts/` — `reroot-edit.js` (root+parent+reframe), `reroot-reorder.js` (ordre topo + renum).

Source décompilée KSP : `D:\ksp-decompiled` (stable, hors dépôt).
