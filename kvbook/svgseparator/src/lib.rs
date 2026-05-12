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

/// Separator core.
/// Modifies 'parent' in-place to remove unnecessary elements, and then re-add them one by one.
/// Since this happens at every level, we know in any given resultant parse-tree relevant stuff was kept.
/// We can use this to provide an extremely precise separation.
/// An important extra attribute is that we preserve the parentage, which might dodge some annoying resvg quirks.
pub fn separator_core(
    xot: &mut Xot,
    zygote_root: Node,
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
                separator_core(xot, zygote_root, *v, submit);
            }
            Handling::Render => {
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
                    let data_fill = xot.to_string(zygote_root).expect("should to_string");

                    // pass 2
                    xot.set_attribute(*v, stroke, stroke_string);
                    xot.set_attribute(*v, fill, "none");
                    let data_stroke = xot.to_string(zygote_root).expect("should to_string");

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
                    let data = xot.to_string(zygote_root).expect("should to_string");
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
            }
        }
        // we finally delete the node, since it's not needed anymore and we've kind of broken the ordering anyways
        _ = xot.remove(*v);
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
        separator_core(&mut xot, root, doc, submit);
    }
    Ok(())
}
