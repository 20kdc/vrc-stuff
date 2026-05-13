use std::collections::{BTreeMap, BTreeSet};
use xot::{Node, Xot};

/// Wraps usvg processing.
pub fn separator_usvg(src_text: &str) -> Result<String, usvg::Error> {
    let src_usvg = usvg::Tree::from_str(
        src_text,
        &usvg::Options {
            font_family: "Liberation Serif".to_string(),
            ..Default::default()
        },
    )?;
    Ok(src_usvg.to_string(&usvg::WriteOptions {
        indent: usvg::Indent::None,
        ..usvg::WriteOptions::default()
    }))
}

#[derive(Clone, Copy, PartialOrd, Ord, PartialEq, Eq, Debug)]
pub enum ContentKind {
    Stroke,
    Fill,
    Image,
}

#[derive(Clone, Copy)]
enum Handling {
    Descend,
    Render,
}

/// This is the state the separator uses to do separating.
pub struct SeparatorState {
    /// This is the defs node. We create an empty one if it doesn't exist, just for consistency.
    /// This is emptied out and then defs are re-implanted as needed.
    pub defs_node: Node,
    /// This is the root node. This is what we serialize.
    pub root: Node,
    /// This is a set of defs nodes and their transitive dependencies.
    /// We detach these to prevent an unfortunate problem where a page with a lot of defs can choke everything.
    /// This requires a lot of awkward bookkeeping and generally sucks to implement.
    /// Something to note is that we can filter defs if need be and leave others alone.
    pub defs: BTreeMap<String, (Node, BTreeSet<String>)>,
}

impl SeparatorState {
    /// Find defs dependencies.
    /// We use a few (questionable) heuristics here, namely relying on the defs table for the most part.
    pub fn svg_dependencies(&self, xot: &Xot, node: Node, deps: &mut BTreeSet<String>) {
        if xot.is_element(node) {
            for v in xot.attributes(node).values() {
                // try: direct
                if self.defs.contains_key(v) {
                    deps.insert(v.to_string());
                }
                // # prefix
                if let Some(no_hash) = v.strip_prefix("#") {
                    if self.defs.contains_key(no_hash) {
                        deps.insert(no_hash.to_string());
                    }
                }
                // write_func_iri and filters
                for word in v.split_whitespace() {
                    if let Some(no_func) =
                        word.strip_prefix("url(#").and_then(|v| v.strip_suffix(")"))
                    {
                        if self.defs.contains_key(no_func) {
                            deps.insert(no_func.to_string());
                        }
                    }
                }
            }
        }
        for v in xot.children(node) {
            self.svg_dependencies(xot, v, deps);
        }
    }

    /// Find deps of deps.
    pub fn svg_dependencies_recurse(&self, deps: &mut BTreeSet<String>) {
        // add indirect dependencies
        // the outer loop will cause all dependencies to eventually propagate
        let prev_deps: Vec<String> = deps.iter().map(|v| v.clone()).collect();
        for dep in &prev_deps {
            if let Some(dep) = self.defs.get(dep) {
                deps.extend(dep.1.iter().map(|v| v.clone()));
            }
        }
    }

    pub fn new(xot: &mut Xot, root: Node, doc: Node) -> SeparatorState {
        let mut defs_base: Option<Node> = None;
        for v in xot.children(doc) {
            if !xot.is_element(v) {
                continue;
            }
            if xot.local_name_str(xot.get_element_name(v)).eq("defs") {
                defs_base = Some(v);
            }
        }
        let defs_node = defs_base.unwrap_or_else(|| {
            // missing defs element, add (for internal consistency)
            let name = xot.add_name("defs");
            let defs_new = xot.new_element(name);
            _ = xot.prepend(doc, defs_new);
            defs_new
        });

        // separate out defs
        // this is where we choose what we consider a valid def to try and split
        let mut defs: BTreeMap<String, (Node, BTreeSet<String>)> = BTreeMap::new();
        let defs_children: Vec<Node> = xot.children(defs_node).collect();
        for def in defs_children {
            if xot.is_removed(def) {
                continue;
            }
            if !xot.is_element(def) {
                continue;
            }
            let idname = xot.add_name("id");
            if let Some(idname) = xot.get_attribute(def, idname) {
                // let def_kind = xot.local_name_str(xot.get_element_name(def));
                defs.insert(idname.to_string(), (def, BTreeSet::new()));
                _ = xot.detach(def);
            }
        }

        let mut separator = SeparatorState {
            defs_node,
            root,
            defs,
        };

        // calculate transitive dependencies
        // we handle this by looping through and adding dependencies of all direct dependencies until settled
        let def_keys: Vec<String> = separator.defs.keys().map(|v| v.clone()).collect();
        let mut is_first_pass = true;
        let mut current_set = BTreeSet::new();
        loop {
            let mut altered: bool = false;
            for key in &def_keys {
                // extract dependencies table and get def node
                let def_access_tmp = &mut separator.defs.get_mut(key).unwrap();
                let def_node = def_access_tmp.0;
                std::mem::swap(&mut current_set, &mut def_access_tmp.1);

                // run dependency extraction
                if is_first_pass {
                    // add direct dependencies
                    separator.svg_dependencies(xot, def_node, &mut current_set);
                    // since we start with no dependencies, any dependency is a new dependency
                    if !current_set.is_empty() {
                        altered = true;
                    }
                } else {
                    let old_len = current_set.len();
                    separator.svg_dependencies_recurse(&mut current_set);
                    // if any new deps are found, then the set will be bigger than before
                    if current_set.len() > old_len {
                        altered = true;
                    }
                }

                // replace dependencies table
                std::mem::swap(
                    &mut current_set,
                    &mut separator.defs.get_mut(key).unwrap().1,
                );
            }
            // no further propagation?
            if !altered {
                break;
            }
            is_first_pass = false;
        }

        separator
    }

    /// Separator core.
    /// Modifies 'parent' in-place to remove unnecessary elements, and then re-add them one by one.
    /// Since this happens at every level, we know in any given resultant parse-tree relevant stuff was kept.
    /// We can use this to provide an extremely precise separation.
    /// An important extra attribute is that we preserve the parentage, which might dodge some annoying resvg quirks.
    pub fn separator_core(
        &self,
        xot: &mut Xot,
        parent: Node,
        submit: &mut dyn FnMut((String, ContentKind)),
    ) {
        //println!("obj {:?}", parent);
        let saved: Vec<Node> = xot.children(parent).collect();
        let mut reinject: Vec<(Handling, Node)> = Vec::new();
        for v in &saved {
            //println!(" - {:?}", v);
            if xot.is_removed(*v) {
                //println!("   mysteriously removed sight-unseen (text merging?)");
                continue;
            }
            // none: preserve
            let handling: Option<Handling> = {
                if !xot.is_element(*v) {
                    None
                } else {
                    let name = xot.local_name_str(xot.get_element_name(*v));
                    // println!("{}", name);
                    if name.eq("svg") {
                        Some(Handling::Descend)
                    } else if name.eq("g") {
                        Some(Handling::Descend)
                    } else if name.eq("defs") {
                        None
                    } else {
                        Some(Handling::Render)
                    }
                }
            };
            if let Some(handling) = handling {
                _ = xot.detach(*v);
                reinject.push((handling, *v));
            }
        }
        let fill = xot.add_name("fill");
        let stroke = xot.add_name("stroke");
        let paint_order = xot.add_name("paint-order");
        for (handling, v) in &reinject {
            _ = xot.append(parent, *v);
            match handling {
                Handling::Descend => {
                    self.separator_core(xot, *v, submit);
                }
                Handling::Render => {
                    // add-deps phase
                    let mut deps = BTreeSet::new();
                    self.svg_dependencies(xot, self.root, &mut deps);
                    self.svg_dependencies_recurse(&mut deps);
                    let deps_nodes: Vec<Node> = deps
                        .iter()
                        .map(|dep| {
                            if let Some(v) = self.defs.get(dep) {
                                _ = xot.append(self.defs_node, v.0);
                                Some(v.0)
                            } else {
                                None
                            }
                        })
                        .flatten()
                        .collect();
                    // usvg sets this to either "stroke" or "stroke fill" and assumes fill is default as usual.
                    // It also sets the stroke and fill attributes to "none" as relevant.
                    let stroke_str = xot.get_attribute(*v, stroke).unwrap_or("none");
                    let fill_str = xot.get_attribute(*v, fill).unwrap_or("none");
                    let has_stroke = !stroke_str.eq("none");
                    let has_fill = !fill_str.eq("none");
                    if has_stroke && has_fill {
                        // stroke and fill on one object!

                        // backup anything important before mutation happens
                        let stroke_first = xot
                            .get_attribute(*v, paint_order)
                            .unwrap_or("fill")
                            .starts_with("stroke");
                        let stroke_string = stroke_str.to_string();

                        // pass 1
                        xot.set_attribute(*v, stroke, "none");
                        let data_fill = xot.to_string(self.root).expect("should to_string");

                        // pass 2
                        xot.set_attribute(*v, stroke, stroke_string);
                        xot.set_attribute(*v, fill, "none");
                        let data_stroke = xot.to_string(self.root).expect("should to_string");

                        // submit in draw order
                        if stroke_first {
                            submit((data_stroke, ContentKind::Stroke));
                            submit((data_fill, ContentKind::Fill));
                        } else {
                            submit((data_fill, ContentKind::Fill));
                            submit((data_stroke, ContentKind::Stroke));
                        }
                    } else {
                        let name = xot.local_name_str(xot.get_element_name(*v));
                        // default
                        let data = xot.to_string(self.root).expect("should to_string");
                        submit((
                            data,
                            if name.eq("image") {
                                ContentKind::Image
                            } else if has_stroke {
                                ContentKind::Stroke
                            } else {
                                ContentKind::Fill
                            },
                        ));
                    }
                    // detach deps
                    for dep in deps_nodes {
                        _ = xot.detach(dep);
                    }
                }
            }
            // we finally delete the node, since it's not needed anymore and we've kind of broken the ordering anyways
            _ = xot.remove(*v);
        }
    }
}

/// Separator interior driver.
pub fn separator_main(
    src_text: &str,
    submit: &mut dyn FnMut((String, ContentKind)),
) -> Result<(), xot::ParseError> {
    let mut xot = Xot::new();
    let root = xot.parse(&src_text)?;
    if let Ok(doc) = xot.document_element(root) {
        let state = SeparatorState::new(&mut xot, root, doc);
        state.separator_core(&mut xot, doc, submit);
    }
    Ok(())
}
