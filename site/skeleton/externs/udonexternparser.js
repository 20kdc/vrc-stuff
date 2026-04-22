window.UDONTYPE_MAXLEN = 0;
for (var key in UDON_API["types"])
	if (key.length > window.UDONTYPE_MAXLEN)
		window.UDONTYPE_MAXLEN = key.length;


/// Parses the ID of an extern.
/// @param {string} src
function udonExternIDParse(src) {
	//
	// -- THIS FUNCTION SHOULD BE KEPT IN SYNC WITH udon/kudoninfo/src/externs.rs --
	//
	var wm = src.split(".");
	if (wm.length != 2)
		throw new Error("Must have exactly one '.': " + src);
	var wrapperName = wm[0];
	if (!wm[1].startsWith("__"))
		throw new Error("Method name no '__' prefix: " + src);
	var presfx = wm[1].substring(2);
	var methodNameSplit = presfx.indexOf("__");
	if (methodNameSplit < 0)
		throw new Error("Method name no '__' suffix: " + src);
	var methodName = presfx.substring(0, methodNameSplit);
	var remainder = presfx.substring(methodNameSplit + 2);
	var parameters = [];
	// only perform parameter search if remainder actually contains such a divider
	// if it doesn't (zero-parameter method) then everything is the return type
	if (remainder.indexOf("__") >= 0) {
		// remainder is a string of the form:
		// TMP_Dropdown_SystemInt32__SystemVoid
		// we have two strategies at our disposal, type database matching and underscore yolo
		// type database matching ensures we catch TMP classes
		// underscore yolo ensures we catch generics
		while (remainder.length > 0) {
			// found return type!
			if (remainder.startsWith("__")) {
				remainder = remainder.substring(2);
				break;
			} else {
				// if it wasn't __, then we need to remove _
				// which might not exist, because this might be the first arg
				if (remainder.startsWith("_"))
					remainder = remainder.substring(1);
				// type database matching
				// notably we need to leave room for an underscore
				var typeGuard = remainder.length - 1;
				if (typeGuard > UDONTYPE_MAXLEN)
					typeGuard = UDONTYPE_MAXLEN;
				while (typeGuard > 0) {
					if (UDON_API["types"][remainder.substring(0, typeGuard)] !== void 0) {
						// since suffixes can crop up, type database matching only provides a guard
						break;
					}
					typeGuard -= 1;
				}
				// underscore yolo
				// notably, we already removed any underscore at the start
				// we ignore underscores that appear before the guard set by type database matching (if any)
				var underscore = remainder.indexOf("_", typeGuard);
				if (underscore >= 0) {
					parameters.push(remainder.substring(0, underscore));
					remainder = remainder.substring(underscore);
				} else {
					// force break. maybe we should error here, this shouldn't happen
					break;
				}
			}
		}
	}
	return {
		wrapperName: wrapperName,
		methodName: methodName,
		parameters: parameters,
		returnType: remainder
	};
}

/// Parses the whole extern.
function udonExternParse(typeName, externName, externObj) {
	//
	// -- THIS FUNCTION SHOULD BE KEPT IN SYNC WITH udon/kudoninfo/src/externs.rs --
	//
	var nameParsed = udonExternIDParse(externName);

	var hasReturn = nameParsed.returnType != "SystemVoid";

	// method has a generic param if it has an undocumented SystemType input that is called "type"
	var couldHaveGenericParam = true;
	for (var i = 0; i < nameParsed["parameters"].length; i++) {
		if (nameParsed["parameters"][i] == "SystemType") {
			couldHaveGenericParam = false;
			break;
		}
	}
	var hasGenericParam = false;
	if (couldHaveGenericParam) {
		for (var j = 0; j < externObj["parameters"].length; j++) {
			var p = externObj["parameters"][j];
			if (p[0] != "type")
				continue;
			if (p[1] != "SystemType")
				continue;
			if (p[2] != "IN")
				continue;
			hasGenericParam = true;
			break;
		}
	}

	// determine if method is static
	// we assume it's static, and then decide otherwise if it looks not static
	var methodStatic = true;
	if (externObj["parameters"][0] !== void 0) {
		var p = externObj["parameters"][0];
		if (p[0] == "instance" && p[1] == typeName && p[2] == "IN")
			methodStatic = false;
	}

	var metadata = [];
	if (!methodStatic) {
		metadata.push(["THIS", null]);
	}
	for (var i = 0; i < nameParsed.parameters.length; i++) {
		metadata.push(["REGULAR", nameParsed.parameters[i]]);
	}
	if (hasGenericParam) {
		metadata.push(["GENERIC", null]);
	}
	if (hasReturn) {
		metadata.push(["RETURN", nameParsed.returnType]);
	}

	if (metadata.length != externObj["parameters"].length)
		throw new Error("Metadata length mismatch [" + metadata + "] [" + externObj["parameters"] + "]");

	var parameters = [];
	for (var i = 0; i < externObj["parameters"].length; i++) {
		var p = externObj["parameters"][i];
		var m = metadata[i];
		parameters.push({
			name: p[0],
			type: p[1],
			dir: p[2],
			role: m[0],
			signatureType: m[1]
		});
	}

	return {
		associatedType: typeName,
		name: externName,
		nameParsed: nameParsed,
		methodStatic,
		parameters,
		hasGenericParam,
		hasReturn,
	};
}

function udonExternType(eName) {
	for (var key in UDON_API["types"]) {
		var tmp = UDON_API["types"][key]["externs"][eName];
		if (tmp !== void 0) {
			// found type
			return key;
		}
	}
	return null;
}
