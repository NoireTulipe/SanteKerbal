"""Crée le zip prêt à installer de KerTech : release/KerTech-<version>.zip

Contenu : GameData/KerTech (sans PluginData, propre à chaque joueur), Module Manager (copié depuis
le dossier de KSP), et le mode d'emploi LISEZMOI.txt.

Usage : python tools/package.py [dossier_KSP]
(à lancer après « dotnet build src -c Release », pour que la DLL soit à jour)
"""
import os
import re
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KSP = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files\GOG Galaxy\Games\Kerbal Space Program"

version = re.search(r"<Version>([^<]+)</Version>", open(os.path.join(ROOT, "src", "KerTech.csproj"), encoding="utf-8").read()).group(1)
mod_dir = os.path.join(ROOT, "GameData", "KerTech")
dll = os.path.join(mod_dir, "Plugins", "KerTech.dll")
if not os.path.exists(dll):
    sys.exit("KerTech.dll absente : compiler d'abord (dotnet build src -c Release)")

mm = [f for f in os.listdir(os.path.join(KSP, "GameData")) if re.match(r"ModuleManager.*\.dll$", f)]
if not mm:
    sys.exit("Module Manager introuvable dans " + os.path.join(KSP, "GameData"))

os.makedirs(os.path.join(ROOT, "release"), exist_ok=True)
out = os.path.join(ROOT, "release", f"KerTech-{version}.zip")
count = 0
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for base, dirs, files in os.walk(mod_dir):
        dirs[:] = [d for d in dirs if d != "PluginData"]
        for f in files:
            full = os.path.join(base, f)
            z.write(full, os.path.join("GameData", "KerTech", os.path.relpath(full, mod_dir)))
            count += 1
    z.write(os.path.join(KSP, "GameData", mm[0]), os.path.join("GameData", mm[0]))
    z.write(os.path.join(ROOT, "LISEZMOI.txt"), "LISEZMOI.txt")
print(f"{out} : {count} fichiers du mod + {mm[0]} + LISEZMOI.txt ({os.path.getsize(out) / 1e6:.1f} Mo)")
