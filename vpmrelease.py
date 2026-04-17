#!/usr/bin/env python3

# VPM release assistant

import subprocess
import sys
import os
import json
import hashlib

assert len(sys.argv) == 2

def read_json(path):
	f = open(path, "r")
	r = json.load(f)
	f.close()
	return r

# read JSON!
pkg_path = sys.argv[1]
pkg_json = read_json(pkg_path + "/package.json")
pkg_name = pkg_json["name"]
pkg_display_name = pkg_json["displayName"]
pkg_version = pkg_json["version"]
assert isinstance(pkg_name, str)
# VRC Creator Companion is less resilient than ALCOM, names are blank if nothing's given here
assert isinstance(pkg_display_name, str)
assert isinstance(pkg_version, str)
assert isinstance(pkg_json["author"], dict)
assert isinstance(pkg_json["author"]["name"], str)

vpm_json = read_json("vpm/index.json")

URL_PREFIX = vpm_json["url"].replace("/index.json", "/")

pkg_zip_name = pkg_name + "-" + pkg_version + ".zip"

# early JSON setup
if not pkg_name in vpm_json["packages"]:
	vpm_json["packages"][pkg_name] = {"versions": {}}
vpm_versions = vpm_json["packages"][pkg_name]["versions"]
if pkg_version in vpm_versions:
	raise Exception("Can't create version " + pkg_version + " that already exists")

# create ZIP
try:
	os.unlink("vpm/" + pkg_zip_name)
except:
	pass
inverse_pkg_path = "../" * (pkg_path.count("/") + 1)
subprocess.check_call(["zip", "-r", inverse_pkg_path + "vpm/" + pkg_zip_name, "."], cwd = pkg_path, stdin = None, stdout = None, stderr = None)

pkg_json["zipSHA256"] = hashlib.file_digest(open("vpm/" + pkg_zip_name, "rb"), "sha256").hexdigest()
pkg_json["url"] = URL_PREFIX + pkg_zip_name

vpm_versions[pkg_version] = pkg_json

# write JSON!
index_f = open("vpm/index.json", "w")
json.dump(vpm_json, index_f, indent = "\t")
index_f.close()

