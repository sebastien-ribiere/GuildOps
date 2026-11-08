# Écrit dans un fichier texte les licences du moteur, EXACTEMENT telles que le binaire Godot les fournit
# (Engine.get_license_text / get_license_info / get_copyright_info) : source vérifiable pour THIRD_PARTY_NOTICES.txt.
# usage : godot-mono --headless --script tools/dump-godot-licenses.gd -- --out=<fichier>
extends SceneTree

func _init() -> void:
	var out := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="):
			out = arg.substr(6)
	if out == "":
		push_error("--out=<fichier> requis")
		quit(2)
		return
	var v := Engine.get_version_info()
	var text := "GODOT ENGINE %s.%s.%s %s - licences fournies par le moteur lui-même\n" % [v.major, v.minor, v.patch, v.status]
	text += "=".repeat(78) + "\n\n" + Engine.get_license_text() + "\n\n"
	text += "=".repeat(78) + "\nCOMPOSANTS TIERS : COPYRIGHT\n" + "=".repeat(78) + "\n\n"
	for entry in Engine.get_copyright_info():
		text += "## " + str(entry.name) + "\n"
		for part in entry.parts:
			text += "   Fichiers : " + ", ".join(part.files) + "\n"
			text += "   Copyright : " + " | ".join(part.copyright) + "\n"
			text += "   Licence : " + str(part.license) + "\n"
		text += "\n"
	text += "=".repeat(78) + "\nTEXTES DES LICENCES\n" + "=".repeat(78) + "\n\n"
	var info := Engine.get_license_info()
	var names := info.keys()
	names.sort()
	for n in names:
		text += "### " + str(n) + "\n\n" + str(info[n]) + "\n\n"
	var f := FileAccess.open(out, FileAccess.WRITE)
	if f == null:
		push_error("écriture impossible : " + out)
		quit(3)
		return
	f.store_string(text)
	f.close()
	print("LICENCES_DUMP_OK ", out, " ", text.length(), " caractères")
	quit(0)
