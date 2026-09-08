# « La base monte quand je recharge » — ce n'est pas le mod

Symptôme : on pose une pièce en construction EVA, on sauvegarde, on recharge, et **toute la
structure a bougé verticalement** — typiquement une ancre dont les clous sortent de terre. Répété,
ça donne l'impression d'une base qui décolle un peu plus à chaque segment.

**Ce n'est pas le mod.** Établi le 2026-09-03, et la conclusion vaut d'être connue avant de
soupçonner le placement : c'est un défaut de KSP, reproductible sans toucher au sol.

## La cause

`ModuleCargoPart.MakePartSettle` **gonfle temporairement la masse** d'une pièce qu'on vient de
reposer, pour qu'elle se stabilise au lieu de glisser :

```csharp
preSettlingPrefabMass = base.part.prefabMass;
...
base.part.prefabMass = 20f;      // 4f si le corps a GeeASL >= 1.1
```

La valeur d'origine n'est restaurée qu'à la **fin de la coroutine**, après `kinematicDelay` puis, le
cas échéant, l'attente d'arrêt du mouvement (`settleDelay`) :

```csharp
yield return new WaitForSeconds(kinematicDelay);
...
base.part.prefabMass = preSettlingPrefabMass;
```

Il existe donc une fenêtre de plusieurs secondes pendant laquelle la pièce pèse **20 tonnes**.
`Vessel.altitude` étant mesurée au centre de masse, une sauvegarde faite dans cette fenêtre
enregistre une altitude calculée sur un centre de masse aberrant — quasiment celui de la pièce
lâchée. Au rechargement la masse est redevenue normale, le centre de masse est ailleurs, et KSP
repositionne le vaisseau pour l'amener à l'altitude enregistrée. Toute la structure se décale.

⚠️ **Confiance** : le chemin de code est vérifié dans le décompilé et le contournement est confirmé
en jeu (voir plus bas), mais le maillon « masse gonflée → centre de masse → altitude enregistrée »
n'a **pas** été mesuré directement. C'est une inférence, solide mais non prouvée.

## Le contournement

**Attendre 10 à 15 secondes après avoir posé une pièce avant de sauvegarder.** Confirmé en jeu :
avec l'attente, l'ancre reste au sol ; sans elle, elle monte.

C'est le genre de défaut qui aurait sa place chez KSPCommunityFixes, qui corrige déjà un problème
voisin sur la masse en construction EVA (`EVAConstructionMass.cs`, sur
`EVAConstructionModeEditor.PickupPart`).

## Comment on l'a établi, et pourquoi c'est instructif

Trois observations successives ont éliminé le mod, dans cet ordre :

1. **Réservoir laissé en l'air, sauvegarde non modifiée** → rien ne bouge.
2. **Réservoir *monté* puis sauvegardé** → l'ancre monte quand même. Or dans ce cas le mod journalise
   `nothing in the way` et laisse passer le déplacement entier : il ne fait **rien**. Le placement
   est donc hors de cause.
3. **Saisir la pièce et la reposer sans la déplacer** → l'ancre monte encore. La géométrie n'a aucun
   rôle ; seule la manipulation compte.

## Fausses pistes — mesurées et écartées

- **Le clamp d'altitude au chargement.** `Vessel.Load` remonte un vaisseau posé à la hauteur PQS
  s'il est en dessous ([`Vessel.cs:4054`](file:///d:/ksp-decompiled/Vessel.cs#L4054) →
  [`getCorrectedLandedAltitude`](file:///d:/ksp-decompiled/Vessel.cs#L6785), un `Math.Max` qui ne
  redescend jamais). **Mesuré : `lifted by 0.000 m` sur les dix vaisseaux de la scène.** Il ne se
  déclenche pas.
- **Une ancre enfoncée que la physique extrait.** Cette explication a été écrite ici puis
  **infirmée**. Elle reposait sur une mesure fausse : le point bas des colliders était comparé à
  `TerrainAltitude`, c'est-à-dire au champ de hauteur **analytique PQS**, et non au maillage de
  collision. L'écart entre ces deux surfaces (plusieurs décimètres, cf. le `CLAUDE.md` parent) avait
  été pris pour un enfoncement de 37 cm. L'observation 1 ci-dessus la contredit de toute façon.
- **Une garde au sol insuffisante.** Testée au maximum (10 cm, visible dans les logs) : l'ancre monte
  pareil.

Leçon de méthode : trois hypothèses solides, chacune appuyée sur du code décompilé, et **les trois
fausses**. Ce sont à chaque fois une mesure ou une observation en jeu qui ont tranché, jamais le
raisonnement.
