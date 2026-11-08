# GuildOps — B5.1 : matrice de conformité et preuves de validation

> Branche `feat/b5-1-ux-polish`, après synchronisation avec `main` (fusion de `origin/main`, qui contenait déjà la spécification via la PR #8).
> Cadre : `docs/phase-b5-1-ux-polish.md` et `docs/phase-b5-visual-implementation.md`. **Aucune modification** de `src/GuildOps.Core/`, de `game-data/` ni des tests métier. Revue humaine requise : B5.1 n'est pas déclarée terminée par ce document.
> Environnement des mesures : Linux x86_64, session X11 réelle (`DISPLAY=:1`), Godot 4.7.2 stable **mono** (rendu Compatibility), .NET SDK 10.0.114, le 8 novembre 2026.

## 1. Matrice de conformité

Légende des preuves : **C** = contrôle automatique exécuté dans le Godot réel (`*_CHECK` de `AutoPlay`) ; **S** = capture d'écran réelle (`docs/b5-1-captures/`) ; **T** = test .NET. « Reste » = travail non réalisé ou limite connue.

### B5.1-A — Typographie et composants partagés

| Exigence | Implémentation existante | Preuve | Reste |
|---|---|---|---|
| Deux styles typographiques centraux (titre Silkscreen, corps lisible accentué) | `UiKit` : Silkscreen 8 px (titres, boutons, badges), Jersey 15 (OFL, `OFL-Jersey15.txt` fourni) comme défaut du thème (commit `c7584b4`) | S `apres/02`, `apres/04` ; C `LAYOUT_CHECK` : 0 glyphe manquant dans toutes les vues | — |
| Aucun glyphe cassé | **Corrigé dans cette PR** : ni Jersey 15 ni Silkscreen ne contiennent `→ ←` (le contrôle de glyphes l'a révélé : ces flèches ne s'affichaient que par repli sur une police système). Remplacées par `->` / `<-` ; `UiKit.Plain` applique la même substitution aux textes venus de Core (journal, verdicts) | C `LAYOUT_CHECK … glyphes-manquants=0` (étendu aux `Button`) | Le texte brut de Core n'est pas modifié, seulement son affichage |
| Panneaux sans artefact | **Corrigé** : les PNG `panel_*_9slice` portent une ligne décorative en bas de la zone centrale ; étirée, elle formait une bande brune épaisse dans tous les grands panneaux (missions, réclamations, bilan, journal). Elle est rattachée à la bordure basse (marge de 9 px). Fond de vue uni (le gris par défaut du moteur apparaissait entre les panneaux) | S avant `avant/04`, `avant/05`, `avant/07` / après `apres/08`, `apres/12`, `apres/16` | — |
| Focus / sélection visibles au-delà de la couleur | `CardButton` : cadre doré épais + coche ; `FocusBox` 2 px | S `apres/04b` ; C `KEYBOARD_FLOW_CHECK` | — |
| HUD et toasts lisibles à 640 × 360, sans clipping sur valeurs longues | HUD inchangé ; la valeur la plus longue observée (`20 or 77 cuivres`) s'affiche en entier ; le toast tronque par ellipse les messages longs (texte complet en infobulle) | S `apres/16`, `apres/22` | Le HUD n'est pas dans le balayage automatique ; messages de toast longs tronqués (limite assumée) |
| Acceptance : « Mme Chausson… », « Prudence ProductOwner » lisibles | Description sur 2 lignes max ; noms sur 2 lignes sans ellipse | C `LAYOUT_CHECK vue=Board` et `vue=Roster` : `tronqués=0` ; S `apres/02`, `apres/04` | — |

### B5.1-B — Tableau des contrats et panneau d'affectation

| Exigence | Implémentation existante | Preuve | Reste |
|---|---|---|---|
| Quatre offres visibles sans scroll, titre/catégorie/énergie/prime/description (2 lignes) | `Cards.OfferRow` (`c7584b4`) | C `vue=Board cartes=4 hors-écran=0 tronqués=0` ; S `apres/02` | — |
| Catégories distinguables par texte + couleur ; sélection et « acceptée » distincts | Pastille texte + couleur ; sélection = fond grenat + cadre doré + coche ; acceptée = pastille « Acceptée » + coche | S `apres/03`, `apres/03b` | — |
| Panneau d'affectation : guide en 2 étapes + bouton contextuel quand quête ou héros manque | **`AssignmentPanel.cs` réécrit** (il n'avait reçu qu'un changement de titre) : indicateur d'étapes `1 QUÊTE` / `2 HÉROS` (coche + cadre doré sur l'étape en cours), texte de guide selon ce qui manque, bouton « Choisir une quête -> » / « Choisir un héros -> » / « Changer de … » masqué quand il ne mène nulle part | S `apres/02` (aucune sélection), `apres/03` (quête choisie) | — |
| Héros, coût, énergie, chance, contrepartie | Verdict, **chance en grand + barre + décomposition** (`ChanceBreakdown` de Core), engagement / énergie / prime, notes de traits, récapitulatif quête et héros (spécialité, niveau, énergie, trait, atout, contrepartie) | S `apres/05` | — |
| Raisons d'impossibilité distinctes | Un motif par ligne (`AssignmentCheck.Reasons` → `RefusalMessages`), icône d'alerte, verdict rouge « AFFECTATION IMPOSSIBLE » | S `apres/06` ; C `REFUSED_ASSIGN_CHECK` | — |
| Décisions issues de `GameSession.Preview()` | Aucun calcul dans l'interface : `Preview`, `PreparationCheck` ; le bouton « Affecter » = `check.CanAssign` | C `REFUSED_ASSIGN_CHECK` : refus sans mutation | — |
| Bouton de régénération lisible | Inchangé (libellé coût et restriction) | S `apres/02` | — |

### B5.1-C — Roster des douze

| Exigence | Implémentation existante | Preuve | Reste |
|---|---|---|---|
| Grille 4×3, 12 noms entiers (la grille défile verticalement : les 3 rangées recomposées font ~330 px pour 313 px utiles) | `Cards.HeroCard` recomposée, `HeroRoster` dans un `ScrollContainer` (suivi du focus) ; noms sur 2 lignes sans ellipse (« Borborygme Release », « Archibald Platform », « Prudence ProductOwner ») | C `vue=Roster cartes=12 hors-écran=0 tronqués=0` (le contrôle tolère le dépassement par le bas en zone défilante) ; S `apres/04` (rangées 1-2), `apres/04c` (grille défilée, 3ᵉ rangée) | La 3ᵉ rangée n'est visible qu'en défilant |
| États sélectionné / engagé / incompatible distincts sans dépendre de la couleur | **Corrigé** : un héros choisi mais incompatible gardait la coche ✔ ; priorité des marqueurs : engagé ⚠ > incompatible ✖ > choisi ✔ | S `apres/04b` (régénérée avec la nouvelle carte ; ✔ choisi, ⚠ engagés, ✖ incompatibles, parchemin = éligible) | — |
| Hiérarchie portrait → nom → « Spécialité · niv. N » → énergie (gauche) / coût (droite) | **Recomposée** : portrait dégagé (plus aucune pastille dessus), nom dessous, spécialité · niveau sous le nom, énergie/coût en bas ; textes suivis par l'état de la carte (lisibles sur parchemin comme sur fond sombre) | S `apres/04`, `apres/04b`, `apres/04c` | — |

### B5.1-D — Missions et résultats

| Exigence | Implémentation existante | Preuve | Reste |
|---|---|---|---|
| Recomposer l'écran, réduire les espaces | **`QuestResult.cs` réécrit** : liste compacte (212 px, lignes de 36 px dont le sous-titre n'est plus coupé), fiche à droite en zone défilante | S `avant/04` / `apres/07`, `apres/08` | Sous un contenu court, la fiche garde du parchemin vide |
| Statut immédiatement identifiable | Icône par ligne (en cours / réussie / échouée) + bandeau de statut | S `apres/07` | — |
| Mise en scène SUCCÈS / ÉCHEC | Bandeau plein largeur (vert/rouge, icône 24 px, titre 16 px) ; animation cosmétique d'opacité/échelle, sans tirage | S `apres/08`, `apres/10` | — |
| Priorité aux récompenses, énergie, réputation, incidents | Prime, énergie, réputation (avant → après, signe coloré) sous le bandeau ; incident encadré | S `apres/08`, `apres/10` | — |
| Section repliable « Détails du calcul » (tirage, probabilité) | Bouton replié par défaut, ouvert à la demande (clavier compris), focus conservé | S `apres/09`, `apres/11` ; C `DETAILS_CHECK` : ouverture/fermeture sans mutation | Les détails reprennent les valeurs consignées par `Resolve` (tirage, chance, engagement) |
| Lien vers les réclamations | Bouton « Voir la réclamation -> » sous l'incident | S `apres/12` | — |
| Aucun nouveau tirage au rafraîchissement | Seul `GameShell.Resolve` appelle `Resolve` ; un clic répété est refusé par Core | C `REPEAT_RESOLVE_CHECK` : deux `Resolve` + `RefreshAll` + changements de vue → empreinte de Core identique, même `MissionOutcome` | — |
| État vide utile | « AUCUNE MISSION ENGAGÉE » + bouton vers le tableau des contrats | S `apres/17` | — |

### B5.1-E — Journal, trésorerie, réclamations

| Exigence | Implémentation existante | Preuve | Reste |
|---|---|---|---|
| Hiérarchie du bilan | **`DayReport.cs` réécrit** : bannière d'état → objectifs (valeur / objectif depuis `rules.Victory`, barre, variation du jour) → conséquences | S `avant/06` / `apres/14` | — |
| Journal structuré | Regroupé par jour (« JOUR n »), numéro atténué, couleur par type d'événement (texte inchangé) | S `apres/14`, `apres/16` | Couleur par type, pas par signe : le texte de Core n'est pas analysé |
| Objectifs et bilan royal au jour 7 | Bannière « DERNIER JOUR — BILAN ROYAL » ; après `EndDay`, coches ✔/✖ lues dans `FinalReport` (aucune comparaison recalculée) | S `apres/15`, `apres/16`, `apres/22` | — |
| Choix et conséquences des réclamations | **`ComplaintDialog.cs` réécrit** : trois cartes de réponse avec coût et effet sur la réputation annoncés avant validation (`rules.Deltas`, `ComplaintTraitBonus`), origine de la plainte, échéance, motifs de refus de `ComplaintCheck` | S `apres/12`, `apres/13` | — |
| États vides sans règle métier | Messages informatifs (missions, réclamations, journal) | S `apres/17`, `apres/18` | — |

### B5.1-F — Hall

| Exigence | Implémentation existante | Preuve | Reste |
|---|---|---|---|
| Douze sprites répartis, pas alignés en bas | **`GuildHall.cs` réécrit** : 12 positions en 5 petits groupes (devant le panneau, bas-gauche, porte, sous la table, râtelier), hors des zones cliquables, triés par hauteur ; indisponibles grisés | S `avant/01` / `apres/01` | Positions décoratives fixes |
| Zones cliquables calées sur le décor | Rectangles ajustés aux objets (panneau, râtelier, table, porte, coffre) | S `apres/01` | — |
| Libellés intégrés | Plaques de bois/parchemin ancrées sur chaque objet (dorées au survol/focus) + pastille d'état (offres, héros libres, missions, réclamations, trésorerie) dérivée de Core, à la place des étiquettes flottantes | S `apres/01` | — |
| Cinq destinations, clavier, tooltips, états ; pas de pathfinding | Conservés ; infobulles reflétant l'état courant | C `KEYBOARD_CHECK`, `KEYBOARD_FLOW_CHECK` | Le hall n'est pas dans le balayage automatique (vérifié sur capture) |

### Parcours de validation (DoD §4 de la spécification)

| # | Exigence | Résultat | Preuve |
|---|---|---|---|
| 1 | Six vues sans exception UI | Fait | `GODOT_EXIT=0 LOG_ERRORS=0` ; `KEYBOARD_CHECK` |
| 2 | Jour 1 : 4 offres, 12 héros, aucun nom tronqué, preview correcte | Fait | `LAYOUT_CHECK` Board / Roster |
| 3 | Assigner → résoudre → détails → plainte → réponse → fin de journée | Fait | `AUTOPLAY_RESULT` = parité Core (§3) ; captures 05 → 16 |
| 4 | Refus d'affectation, clic de résolution répété | Fait | `REFUSED_ASSIGN_CHECK`, `REPEAT_RESOLVE_CHECK` |
| 5 | 7 jours, victoire et échec selon scénarios de référence | Fait : trois parties de `docs/playtests/` rejouées via l'interface, journaux **identiques ligne à ligne**, + partie inactive | §3 |
| 6 | Clavier seul | Fait (une mission) | `KEYBOARD_FLOW_CHECK` |
| 7 | Captures réelles 1280 × 720, avant/après | Fait | `docs/b5-1-captures/` |
| 8 | `dotnet test`, `dotnet build`, Godot lancé, exports | Fait ; export Windows non exécuté | §2 |
| 9 | Diff limité à Ui / Scenes / polices (+ outils de preuve, docs) | Fait : `GuildOps.Core`, `game-data` et tests inchangés | `git diff --stat origin/main..HEAD` |

## 2. Commandes réellement exécutées

```text
dotnet build GuildOps.sln --nologo                    -> 0 avertissement, 0 erreur (rc 0)
dotnet test  GuildOps.sln --nologo                    -> Réussi : 154, échoué : 0, ignoré : 3, total : 157 (rc 0)
tools/shots.sh <dossier> <godot-mono>                 -> dotnet build + godot --headless --import + scénario (rc 0)
tools/run-autoplay.sh <dossier> <godot-mono> first-fit | greedy-repair | greedy-apologize | greedy-ignore | idle   (rc 0 chacun)
godot --headless --path src/GuildOps.Game --export-release "Linux x64"    -> rc 0 (binaire de 74 Mo)
godot --headless --path src/GuildOps.Game --export-release "Windows x64"  -> rc 0 (exécutable de 110 Mo, NON exécuté : pas de Windows ici)
GuildOps.x86_64 -- --autoplay --shots=<dossier>        -> binaire exporté Linux : rc 0, 16 contrôles OK, AUTOPLAY_RESULT identique
```

- Les **3 tests ignorés** sont les tests lents (rapport d'équilibrage, benchmark) désactivés par défaut (`SlowSupport.cs` : `…=1 dotnet test --filter "Category=Slow"`). Ils n'ont pas été lancés ici.
- Les scripts ne masquent plus aucun code de retour (les anciens `|| true` et le `grep … || true` de `tools/shots.sh` ont été supprimés). `tools/run-autoplay.sh` recompile d'abord (sinon Godot rejouerait une DLL périmée — cela m'est arrivé pendant le travail) et sort non nul si Godot, un contrôle, le journal Godot ou la comparaison de journaux échoue.
- Godot **graphique** réel : disponible et utilisé (X11). Les captures sont des images du viewport 640 × 360 réellement rendues puis agrandies ×2 (plus proche voisin) en 1280 × 720 ; ce ne sont ni des captures headless ni des images conceptuelles.

## 3. Résultats de non-régression

| Partie (graine 1142026) | Résultat observé dans l'interface | Référence |
|---|---|---|
| `first-fit` (parité `UiParityTests`) | jour 7, caisse 2077 cuivres, réputation 87, 18 réussites, 113 événements, redressement réussi | identique à `UiParityTests` (et à l'état d'avant B5.1) |
| `greedy-repair` | caisse 1707, réputation 64, 15 réussites, 107 événements, **redressement réussi** | `docs/playtests/seed-1142026-RepairIfPossible.md` : journal identique (107 lignes) |
| `greedy-apologize` | réputation 54 (seuil 55), 107 événements, **bilan inachevé** | `…-Apologize.md` : identique (107 lignes) |
| `greedy-ignore` | réputation 37, 105 événements, **bilan inachevé** | `…-Ignore.md` : identique (105 lignes) |
| `idle` | 0 réussite, réputation 35, 22 événements, **bilan non réussi** | `PlaytestTests.Doing_nothing_for_seven_days_loses_without_any_error` |

Les parties gloutonnes sont rejouées par `AutoPlay` (qui reproduit la politique du robot de test) mais **toutes leurs commandes passent par les sélections et `GameShell`**, donc par les mêmes chemins que les boutons.

## 4. Ce que les contrôles automatiques ne prouvent pas

- Le balayage (`LAYOUT_CHECK`) mesure : cartes dans l'écran 640 × 360, lignes masquées, ellipses, libellés de bouton plus larges que leur bouton, glyphes absents de la police. Il ne juge ni l'esthétique ni la lisibilité subjective : c'est le rôle de la revue des captures.
- Le parcours clavier couvre une mission complète (hall → contrats → héros → affecter → résoudre), pas chaque vue au clavier.
- Aucun test avec lecteur d'écran ni simulation de daltonisme : seule la règle « jamais la couleur seule » (texte + icône) est appliquée.
- Windows : exporté mais non lancé. macOS : hors périmètre.

## 5. Limitations restantes

1. Les messages longs du toast du bas sont tronqués par une ellipse (texte complet en infobulle).
2. Les polices embarquées n'ont pas de flèches Unicode : `->` / `<-` sont utilisés ; un repli typographique plus fin demanderait une police ou un glyphe supplémentaire (hors périmètre « ne pas remplacer le kit »).
3. La fiche de mission et la fiche de réclamation laissent du parchemin vide quand leur contenu est court (zone défilante plutôt que carte qui se rétracte, pour garder le défilement fiable).
4. Les positions des sprites du hall sont des constantes décoratives ; si le décor change, elles sont à recaler (un commentaire les relie aux zones cliquables).
