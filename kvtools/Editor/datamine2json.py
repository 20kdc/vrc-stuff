#!/usr/bin/env python3
import json
import subprocess

dfile = open("datamine.txt", "r")

data = {}
data_externs = {}
data_types = {}
data_events = {}
data_synctype = []
data["externs"] = data_externs
data["types"] = data_types
data["events"] = data_events
data["sync_types"] = data_synctype

while True:
	decltype = dfile.readline().strip()
	if decltype == "END":
		break
	elif decltype == "EXTERN":
		extern_id = dfile.readline().strip()
		extern_type = dfile.readline().strip()
		extern_id_type = extern_id[:extern_id.find(".")]
		extern_id_signature = extern_id[extern_id.find(".") + 1:]
		extern_parameters = []
		for _ in range(int(dfile.readline().strip())):
			p_name = dfile.readline().strip()
			p_type = dfile.readline().strip()
			p_dir = dfile.readline().strip()
			extern_parameters.append({
				"name": p_name,
				"type": p_type,
				"dir": p_dir,
			})
		data_externs[extern_id] = {
			"id": extern_id,
			"id_type": extern_id_type,
			"id_signature": extern_id_signature,
			"type": extern_type,
			"parameters": extern_parameters
		}
		if not (extern_id_type in data_types):
			# fake replacable static type
			data_types[extern_id_type] = {
				"static": True,
				"name": extern_id_type,
				"odin_name": "",
				"base": "",
				"interfaces": [],
				"sync_type": None
			}
	elif decltype == "EVENT":
		event_name = dfile.readline().strip()
		event_entrypoint = dfile.readline().strip()
		event_parameters = []
		for _ in range(int(dfile.readline().strip())):
			p_name = dfile.readline().strip()
			p_type = dfile.readline().strip()
			event_parameters.append({
				"name": p_name,
				"type": p_type
			})
		data_events[event_entrypoint] = {
			"name": event_name,
			"entrypoint": event_entrypoint,
			"parameters": event_parameters
		}
	elif decltype == "TYPE":
		type_name = dfile.readline().strip()
		type_odin_name = dfile.readline().strip()
		type_base = dfile.readline().strip()
		type_interfaces = []
		for _ in range(int(dfile.readline().strip())):
			type_interfaces.append(dfile.readline().strip())
		data_types[type_name] = {
			"static": False,
			"name": type_name,
			"odin_name": type_odin_name,
			"base": type_base,
			"interfaces": type_interfaces,
			"sync_type": None
		}
		if type_base == "SystemEnum":
			data_types[type_name]["enumValues"] = {}
	elif decltype == "EVAL":
		eval_type = dfile.readline().strip()
		eval_name = dfile.readline().strip()
		eval_value = int(dfile.readline().strip())
		data_types[eval_type]["enumValues"][eval_name] = eval_value
	elif decltype == "SYNCTYPEID":
		st_id = int(dfile.readline().strip())
		st_ty = dfile.readline().strip()
		while len(data_synctype) <= st_id:
			data_synctype.append(None)
		data_synctype[st_id] = {
			"sync_type": st_id,
			"name": st_ty
		}
		data_types[st_ty]["sync_type"] = st_id
	else:
		raise Exception("Unknown decltype " + decltype)

# --

# since we started committing API data to Git, sort_keys=true has become very important
apijsonfile = open("api.json", "w", newline="\n")
apicjsonfile = open("api_c.json", "w", newline="\n")
json.dump(data, apijsonfile, ensure_ascii = False, indent = "\t", sort_keys = True)
json.dump(data, apicjsonfile, ensure_ascii = False, indent = None, separators = (",", ":"), sort_keys = True)
apijsonfile.close()
apicjsonfile.close()

# --

sync_type_table = open("synctypes.md", "w")
sync_type_table.write("| Type Index | Udon Type |\n")
sync_type_table.write("| ---------- | --------- |\n")
for st in data_synctype:
	if st != None:
		sync_type_table.write("| " + str(st["sync_type"]) + " | `" + st["name"] + "` |\n")
sync_type_table.close()

# --

ran_proc = subprocess.run(["xz", "-v", "-z", "api_c.json", "-9", "-c"], stdout = subprocess.PIPE, check = True)
generated_xz = open("../../udon/kudon_apijson/src/api_c.json.xz", "wb")
generated_xz.write(ran_proc.stdout)
generated_xz.close()
