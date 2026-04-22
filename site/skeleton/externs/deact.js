/**
 * It's almost like React, except if it wasn't at all like React.
 * This is a Reactish element building style without a virtual DOM.
 * The first argument is the tag name.
 * Further elements are either children or props.
 */
function h(ty) {
	var props = {};
	var children = [];
	for (var i = 1; i < arguments.length; i++) {
		var val = arguments[i];
		hNormalize(props, children, val);
	}
	var target = document.createElement(ty);
	for (var i = 0; i < children.length; i++) {
		target.appendChild(children[i]);
	}
	for (var key in props) {
		target[key] = props[key];
	}
	return target;
}

function hAppend(target) {
	var props = {};
	var children = [];
	for (var i = 1; i < arguments.length; i++) {
		var val = arguments[i];
		hNormalize(props, children, val);
	}
	for (var i = 0; i < children.length; i++) {
		target.appendChild(children[i]);
	}
	for (var key in props) {
		target[key] = props[key];
	}
	return target;
}

function hNormalize(props, children, val) {
	if (val === null || val === void 0) {
		// do nothing
	} else if (typeof val === "string") {
		children.push(document.createTextNode(val));
	} else if (val instanceof Node) {
		children.push(val);
	} else if (val instanceof Array) {
		for (var i = 0; i < val.length; i++) {
			hNormalize(props, children, val[i]);
		}
	} else {
		// assume props
		Object.assign(props, val);
	}
}
