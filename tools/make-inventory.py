#!/usr/bin/env python3
"""Génère INVENTAIRE_RESSOURCES_ET_LICENCES.md : ce qui est redistribué dans une archive, avec empreintes SHA-256 (bibliothèque standard seule).
usage : python3 tools/make-inventory.py <dossier-de-distribution> <plateforme> <sortie.md>"""
import hashlib, json, sys
from pathlib import Path

dist, platform, out = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
root = Path(__file__).resolve().parents[1]
game = root / "src" / "GuildOps.Game"

def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()

def count(folder: Path, pattern="*.png"):
    files = sorted(f for f in folder.rglob(pattern))
    return len(files), sum(f.stat().st_size for f in files)

lines = [f"# Inventaire des ressources et licences redistribuées — GuildOps B5.3 ({platform})", "",
         "Document généré par `tools/make-inventory.py` à partir du contenu réel de l'archive. Les empreintes sont des SHA-256.", ""]

lines += ["## 1. Programme et environnement d'exécution", "",
          "| Élément | Détail | Licence |", "|---|---|---|"]
exe = next((f for f in dist.iterdir() if f.name.startswith("GuildOps.") and f.suffix in (".exe", ".x86_64")), None)
if exe:
    size = f"{exe.stat().st_size:,}".replace(",", " ")
    lines.append(f"| `{exe.name}` (moteur Godot 4.7.2 .NET, ressources du jeu embarquées) | {size} octets — `{sha(exe)}` | MIT (Godot) ; voir `LICENSES/THIRD_PARTY_NOTICES.txt` |")
data = next((d for d in dist.iterdir() if d.is_dir() and d.name.startswith("data_")), None)
if data:
    for name, role, lic in [("GuildOps.Game.dll", "code du jeu (interface)", "propre au projet (voir §5)"),
                            ("GuildOps.Core.dll", "moteur de règles", "propre au projet (voir §5)"),
                            ("GodotSharp.dll", "liaison C# de Godot", "MIT (Godot)")]:
        f = data / name
        if f.exists():
            lines.append(f"| `{data.name}/{name}` | {role} — `{sha(f)}` | {lic} |")
    n = sum(1 for _ in data.iterdir())
    lines.append(f"| `{data.name}/` : {n} fichiers (environnement d'exécution .NET embarqué : coreclr, hostfxr, System.*, …) | Microsoft .NET (version : voir `GuildOps.Game.runtimeconfig.json`) | MIT (Microsoft) ; voir `LICENSES/DOTNET_LICENSE.txt` et `DOTNET_ThirdPartyNotices.txt` |")
    rc = data / "GuildOps.Game.runtimeconfig.json"
    if rc.exists():
        fw = json.loads(rc.read_text(encoding="utf8")).get("runtimeOptions", {}).get("includedFrameworks") or json.loads(rc.read_text(encoding="utf8")).get("runtimeOptions", {}).get("framework")
        lines += ["", f"Configuration d'exécution : `{json.dumps(fw, ensure_ascii=False)}`"]

lines += ["", "## 2. Typographie", "",
          "| Fichier | Auteur | Licence |", "|---|---|---|"]
for f in sorted((game / "Assets" / "fonts").glob("*.ttf")):
    lines.append(f"| `Assets/fonts/{f.name}` — `{sha(f)}` | © 2020-2025 GGBotNet (Reserved Font Name « Pixeloid ») | SIL Open Font License 1.1 (`LICENSES/OFL-PixeloidSans.txt`) |")

lines += ["", "## 3. Images embarquées dans le jeu", "", "| Lot | Fichiers PNG | Taille | Origine |", "|---|---|---|---|"]
sets = [("Assets/b5_3", "kit B5.3 (titre, scènes, tutoriel, branding, icônes) — manifest : `docs/b5-3/manifest.json`", "docs/b5-3/SOURCES_ET_LICENCES.md"),
        ("Assets/b5_2", "kit B5.2 HD (contrats, effets, ambiance)", "docs/b5-2-integration.md"),
        ("Assets/backgrounds", "décor du hall", "docs/b5-art-kit/README.md"),
        ("Assets/portraits", "portraits des douze héros", "docs/b5-art-kit/README.md"),
        ("Assets/sprites", "sprites des douze héros", "docs/b5-art-kit/README.md"),
        ("Assets/ui", "panneaux, boutons, cadre", "docs/b5-art-kit/README.md"),
        ("Assets/icons", "icônes 16×16 du HUD", "docs/b5-art-kit/README.md")]
total = 0
for folder, label, src in sets:
    n, size = count(game / folder)
    total += n
    lines.append(f"| `{folder}` — {label} | {n} | {size // 1024} Kio | créations du projet GuildOps, sans élément de tiers déclaré (voir `{src}`) |")
lines += ["", f"Total : **{total} PNG**. Les dossiers `sources/` et `previews/` du kit B5.3 ne sont **pas** redistribués.", ""]

lines += ["## 4. Données de jeu (embarquées dans `GuildOps.Core.dll`)", "", "| Fichier | SHA-256 |", "|---|---|"]
for f in sorted((root / "game-data").glob("*.json")):
    lines.append(f"| `game-data/{f.name}` | `{sha(f)}` |")

lines += ["", "## 5. Points ouverts avant diffusion publique ou commerciale", "",
          "- Le dépôt ne contient **aucun fichier de licence pour GuildOps lui-même** : l'éditeur doit choisir et ajouter la licence du code et du contenu.",
          "- `docs/b5-3/SOURCES_ET_LICENCES.md` précise que les droits de diffusion publique/commerciale des illustrations générées au cours du projet doivent être vérifiés par le responsable de la distribution ; aucune attribution individuelle n'est fournie faute de liste validée.",
          "- Aucun son, aucune musique, aucune police autre que Pixeloid Sans n'est redistribué.", ""]
out.write_text("\n".join(lines), encoding="utf8")
print(f"OK {out} ({len(lines)} lignes)")
