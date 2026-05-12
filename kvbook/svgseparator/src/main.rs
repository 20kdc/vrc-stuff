fn main() {
    // we don't bother with lexopt for this. just YOLO something, anything - pt.
    let mut a = std::env::args();
    _ = a.next();
    let src = a.next().expect("expected src");
    let dst = a.next().expect("expected dst");
    assert!(a.next().is_none());
    // stage 1: internally process using usvg
    let src_text = std::fs::read_to_string(&src).expect("source should be readable");
    let src_text2 =
        svgseparator::separator_usvg(&src_text).expect("source should be processable with usvg");
    _ = std::fs::write(&format!("{}/usvg.svg", dst), &src_text2);
    let mut shape_num: usize = 1;
    // stage 2: split
    svgseparator::separator_main(&src_text2, &mut |(svg, _content)| {
        _ = std::fs::write(&format!("{}/s{}.svg", dst, shape_num), &svg);
        shape_num += 1;
    })
    .unwrap();
}
