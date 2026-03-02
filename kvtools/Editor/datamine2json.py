#!/usr/bin/env python3
import json
import subprocess

dfile = open("datamine.txt", "r")

data = {}
data_types = {}
data_events = {}
data_discard_ext = {}
data_synctype = [None] * 256
data["version"] = "(unknown)"
data["types"] = data_types
data["events"] = data_events
# data["discard_ext"] = data_discard_ext
# remove for space, redundant
# data["sync_types"] = data_synctype

max_param_count = 0
externs_count = 0

# -- utils --

def after_the_dot(extern_id):
	return extern_id[extern_id.find(".") + 1:]

# -- import pass --

while True:
	decltype = dfile.readline().strip()
	if decltype == "END":
		break
	elif decltype == "VRCSDKVER":
		data["version"] = dfile.readline().strip()
	elif decltype == "EXTERN":
		extern_id = dfile.readline().strip()
		extern_deftype = dfile.readline().strip()
		extern_declaring = dfile.readline().strip()
		extern_type = dfile.readline().strip()
		extern_parameters = []
		for _ in range(int(dfile.readline().strip())):
			p_name = dfile.readline().strip()
			p_type = dfile.readline().strip()
			p_dir = dfile.readline().strip()
			extern_parameters.append([p_name, p_type, p_dir])
		max_param_count = max(max_param_count, len(extern_parameters))
		data_types[extern_type]["externs"][extern_id] = {
			"declaring": extern_declaring,
			"parameters": extern_parameters
		}
		externs_count += 1
	elif decltype == "DISCARD_EXT":
		extern_id = dfile.readline().strip()
		data_discard_ext[extern_id] = True
	elif decltype == "EVENT":
		event_name = dfile.readline().strip()
		event_entrypoint = dfile.readline().strip()
		event_parameters = []
		for _ in range(int(dfile.readline().strip())):
			p_name = dfile.readline().strip()
			p_type = dfile.readline().strip()
			event_parameters.append([p_name, p_type])
		data_events[event_entrypoint] = {
			"name": event_name,
			"entrypoint": event_entrypoint,
			"parameters": event_parameters
		}
	elif decltype == "TYPE":
		type_name = dfile.readline().strip()
		type_odin_name = dfile.readline().strip()
		type_sync_type = int(dfile.readline().strip())
		type_kind = dfile.readline().strip()
		if type_name in data_types:
			raise Exception("Duplicate type error: " + type_name)
		data_types[type_name] = {
			"name": type_name,
			"kind": type_kind,
			"odin_name": type_odin_name,
			"bases": [],
			"externs": {}
		}
		if type_sync_type != 0:
			data_synctype[type_sync_type] = type_name
			data_types[type_name]["sync_type"] = type_sync_type
	elif decltype == "TYPEBASE":
		type_name = dfile.readline().strip()
		type_base = dfile.readline().strip()
		assert type_base in data_types
		data_types[type_name]["bases"].append(type_base)
	elif decltype == "EVAL":
		eval_type = dfile.readline().strip()
		eval_name = dfile.readline().strip()
		eval_value = int(dfile.readline().strip())
		if not "enum_values" in data_types[eval_type]:
			# retroactively change "kind"
			data_types[eval_type]["kind"] = "ENUM"
			data_types[eval_type]["enum_values"] = {}
		data_types[eval_type]["enum_values"][eval_name] = eval_value
	else:
		raise Exception("Unknown decltype " + decltype)

# -- api.json is dumped before pruning --

apijsonfile = open("api.json", "w", newline="\n")
json.dump(data, apijsonfile, ensure_ascii = False, indent = "\t", sort_keys = True)
apijsonfile.close()

# -- redundancy pruning pass --

pruned_count = 0

# Extern pruning (disabled since untrustworthy)
if False:
	# Ok, this DIDN'T delete the list methods, I forgot those were things VRC didn't expose.
	# However, I panicked when I realized that generics might not be covered properly in this.
	# So holding off on deploying this.
	for dt_name in data_types:
		dt_externs = data_types[dt_name]["externs"]
		for extern_id in list(dt_externs.keys()):
			declarator = dt_externs[extern_id]["declaring"]
			if declarator == "":
				# ineligible: unable to determine
				continue
			if (declarator in data_types) and (declarator != dt_name):
				# function is declared elsewhere and we can check
				checkme = declarator + "." + after_the_dot(extern_id)
				if checkme in data_types[declarator]["externs"]:
					# binding autogen strikes again
					del dt_externs[extern_id]
					pruned_count += 1

# -- redundant metadata removal --

for dt_name in data_types:
	dt_externs = data_types[dt_name]["externs"]
	for extern_id in dt_externs:
		del dt_externs[extern_id]["declaring"]

# --

# since we started committing API data to Git, sort_keys=true has become very important
apicjsonfile = open("api_c.json", "w", newline="\n")
json.dump(data, apicjsonfile, ensure_ascii = False, indent = None, separators = (",", ":"), sort_keys = True)
apicjsonfile.close()

# prune debug
apixjsonfile = open("api_x.json", "w", newline="\n")
json.dump(data, apixjsonfile, ensure_ascii = False, indent = "\t", sort_keys = True)
apixjsonfile.close()

# --

sync_type_table = open("synctypes.md", "w")
sync_type_table.write("| Type Index | Udon Type |\n")
sync_type_table.write("| ---------- | --------- |\n")
for st in range(len(data_synctype)):
	if data_synctype[st] != None:
		sync_type_table.write("| " + str(st) + " | `" + data_synctype[st] + "` |\n")
sync_type_table.close()

# --

ran_proc = subprocess.run(["xz", "-v", "-z", "api_c.json", "-9", "-c"], stdout = subprocess.PIPE, check = True)
generated_xz = open("../../udon/kudon_apijson/src/api_c.json.xz", "wb")
generated_xz.write(ran_proc.stdout)
generated_xz.close()

# --

statistics = open("statistics.md", "w")

statistics.write("# Statistics (`datamine2json.py`)\n")
statistics.write("\n")
statistics.write("SDK version (`com.vrchat.worlds`): `" + data["version"] + "`\n")
statistics.write("\n")

statistics.write("* " + str(max_param_count) + " parameters max.\n")
statistics.write("* " + str(externs_count) + " externs before pruning.\n")
statistics.write("* " + str(pruned_count) + " externs pruned due to having a perfectly good declaration in another type.\n")
statistics.write("* " + str(len(data_types)) + " discovered types.\n")
