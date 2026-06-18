window.bookInitialized = false;

function bFindDocPackage(breakdown, pid) {
	var adj = breakdown.netTypeNoGenerics;
	return "https://docs.unity3d.com/Packages/" + pid + "/api/" + adj + ".html";
}

function bFindDoc(ty) {
	var breakdown = udonTypeBreakdown(ty);

	// too early and `CinemachineICinemachineTargetGroup` fails
	// too late and it's moved to 'Unity' namespace
	if (breakdown.assembly == "Cinemachine")
		return bFindDocPackage(breakdown, "com.unity.cinemachine@2.10");

	if (breakdown.assembly == "Unity.AI.Navigation")
		return bFindDocPackage(breakdown, "com.unity.ai.navigation@1.1");

	if (breakdown.assembly == "Unity.TextMeshPro")
		return bFindDocPackage(breakdown, "com.unity.ugui@2.0");

	if (breakdown.assembly == "Unity.Postprocessing.Runtime")
		return bFindDocPackage(breakdown, "com.unity.postprocessing@2.0");

	if (breakdown.udonType.endsWith("Array"))
		return "https://learn.microsoft.com/en-us/dotnet/api/system.array?view=netstandard-2.1";

	if (breakdown.udonType == "VRCEconomyStore")
		return "https://creators.vrchat.com/economy/sdk/udon-documentation/#methods";

	if (breakdown.udonType == "VRCSDK3ComponentsVRCOpenMenu")
		return "https://creators.vrchat.com/economy/sdk/udon-documentation/#vrcopenmenuopenavatarlisting";

	if (breakdown.udonType.startsWith("VRCEconomy"))
		return "https://creators.vrchat.com/economy/sdk/udon-documentation/#" + breakdown.shortName.toLowerCase();

	if (breakdown.assembly.startsWith("VRC")) {
		var almostReady = breakdown.shortName.toLowerCase().replaceAll("[]", "");
		var almostReadyPlusIdx = almostReady.lastIndexOf("+");
		if (almostReadyPlusIdx != -1)
			almostReady = almostReady.substring(almostReadyPlusIdx + 1);
		return "https://udonsharp.docs.vrchat.com/vrchat-api/#" + almostReady;
	}

	if (breakdown.assembly.startsWith("Unity")) {
		var almostReady = breakdown.netTypeNoGenerics;
		if (almostReady.startsWith("UnityEngine."))
			almostReady = almostReady.substring(12);
		almostReady = almostReady.replaceAll("+", ".");
		almostReady = almostReady.replaceAll("[]", "");
		return "https://docs.unity3d.com/2022.3/Documentation/ScriptReference/" + almostReady + ".html";
	}

	if (breakdown.assembly == "mscorlib" || breakdown.assembly == "System") {
		var adjustment = breakdown.netTypeNoGenerics.toLowerCase();
		adjustment = adjustment.replaceAll("+", ".");
		// 'fix' generics
		var genericFixPos = breakdown.netType.indexOf("`");
		if (genericFixPos != -1)
			adjustment += "-" + breakdown.netType.substring(genericFixPos + 1, genericFixPos + 2);
		return "https://learn.microsoft.com/en-us/dotnet/api/" + adjustment + "?view=netstandard-2.1";
	}
	return null;
}

function BCopyable(text, href) {
	return h(
		"span",
		href ? h("a", {href: href}, h("code", text)) : h("code", text),
		h("button", {className: "copyableButton", onclick: function () {
			navigator.clipboard.writeText(text);
		}}, "📋"),
	)
}

function BTypeList() {
	var list = h("ul");
	for (var key in UDON_API["types"]) {
		list.appendChild(h("li",
			BCopyable(key, "#type:" + key)
		));
	}
	return [
		h("h1", "Type List"),
		list
	];
}

function BType(tName) {
	var ty = UDON_API["types"][tName];
	if (ty) {
		var res = [];
		var baseList = [];
		for (var i = 0; i < ty["bases"].length; i++) {
			var baseTy = ty["bases"][i];
			if (i != 0)
				baseList.push(", ");
			baseList.push(h("a", {href: "#type:" + baseTy}, h("code", baseTy)));
		}
		var doc = bFindDoc(ty);
		res.push(h("div", {id: "typeHeader", className: "typeIntro"}, [
			h("h2", "Type ", BCopyable(tName)),
			h("p", "Kind: ", h("code", ty["kind"])),
			h("p", "Base Types: ", baseList),
			h("p", "OdinSerializer: ", BCopyable(ty["odin_name"])),
			h("p", "Sync Type: ",
				(ty["sync_type"] !== void 0) ?
				BCopyable(String(ty["sync_type"])) :
				"None"
			),
			doc == null ? null : h("p", "Documentation: ", h("a", {href: doc}, h("code", doc)))
		]));
		if (ty["enum_values"]) {
			res.push(h("h2", "Enum Values"));
			for (let key in ty["enum_values"]) {
				res.push(h("p", h("code", key + " = " + ty["enum_values"][key])));
			}
		}
		res.push(h("h2", "Externs"));
		for (var extern in ty["externs"]) {
			var extRes = [];
			var parsed = udonExternParse(tName, extern, ty["externs"][extern]);
			var tags = [];
			if (parsed.methodStatic) {
				tags.push(h("span", {className: "exttag exttagStatic"}, "static"));
				tags.push(" ");
			}
			if (parsed.hasGenericParam) {
				tags.push(h("span", {className: "exttag exttagGeneric"}, "generic"));
				tags.push(" ");
			}
			extRes.push(h("h3", {id: "extern:" + extern}, tags, BCopyable(extern, "#extern:" + extern)));
			var extParams = [];
			for (var i = 0; i < parsed.parameters.length; i++) {
				var p = parsed.parameters[i];
				extParams.push(h("li",
					h("code", p.dir + " " + p.name + " "),
					BCopyable(p.type, "#type:" + p.type)
				));
			}
			extRes.push(h("ul"), extParams);
			res.push(h("div", {className: "genericIntro"}, extRes));
		}
		return res;
	} else {
		return [
			h("h2", "Oopsie!"),
			h("p", "The type you're looking for (", h("code", tName), ") ", h("i", "doesn't exist!"))
		];
	}
}

function bNav(hash) {
	console.log("navigate: " + hash);

	// clear the view
	var elmView = document.getElementById("view");
	while (elmView.firstChild) {
		elmView.firstChild.remove();
	}

	if (hash.startsWith("#type:")) {
		var tName = hash.substring(6);
		hAppend(elmView, BType(tName));
		document.getElementById("typeHeader").scrollIntoView();
	} else if (hash.startsWith("#extern:")) {
		var eName = hash.substring(8);
		var tName = udonExternType(eName);
		if (tName != null) {
			hAppend(elmView, BType(tName));
		} else {
			hAppend(elmView, h("p", "unknown extern, sorry!"));
		}
		// very hacky :3
		document.getElementById(hash.substring(1)).scrollIntoView();
	} else if (hash.startsWith("#search:")) {
		var searchText = hash.substring(8).toLowerCase();
		var list = h("ul");
		var externs = [];
		for (var key in UDON_API["types"]) {
			var typeObj = UDON_API["types"][key];
			if (key.toLowerCase().indexOf(searchText) >= 0) {
				list.appendChild(h("li",
					h("a",
						{href: "#type:" + key},
						"type ",
						h("code", key)
					)
				));
			}
			for (var ext in typeObj["externs"]) {
				if (ext.toLowerCase().indexOf(searchText) >= 0) {
					externs.push(ext);
				}
			}
		}
		for (let i = 0; i < externs.length; i++) {
			var ext = externs[i];
			list.appendChild(h("li",
				h("a",
					{href: "#extern:" + ext},
					"extern ",
					h("code", ext)
				)
			));
		}
		hAppend(elmView, [
			h("h1", "Search Results"),
			list
		]);
	} else {
		hAppend(elmView, BTypeList());
	}
}

window.onhashchange = function () {
	if (window.bookInitialized)
		bNav(location.hash);
};

function initialize() {
	document.getElementById("apiVersion").innerText = UDON_API["version"];
	bNav(location.hash);
	window.bookInitialized = true;
}

function sanityTest() {
	for (var typeName in UDON_API["types"]) {
		var typeObj = UDON_API["types"][typeName];
		for (var externName in typeObj["externs"]) {
			var externObj = typeObj["externs"][externName];
			udonExternParse(typeName, externName, externObj);
		}
	}
}

var searchBoxCurrentTimeout = null;

function searchBoxChanged() {
	if (searchBoxCurrentTimeout !== null)
		clearTimeout(searchBoxCurrentTimeout);
	searchBoxCurrentTimeout = setTimeout(function () {
		var text = document.getElementById("searchBox").value;
		if (text.length >= 4)
			location.hash = "#search:" + text;
	}, 250);
}
