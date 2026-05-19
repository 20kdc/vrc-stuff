fn main() {
    let mut a = std::env::args();
    _ = a.next();
    let src = a.next().expect("expected src");
    let dst = a.next().expect("expected dst");
    assert!(a.next().is_none());
    let pages = inputlib::read(
        &src,
        &inputlib::InputOpts {
            mutool: None,
            mudraw_opts: vec![],
        },
    )
    .unwrap();
    for page in pages.enumerate() {
        println!(" {}", page.0);
        std::fs::write(format!("{}/{}.svg", dst, page.0), page.1).unwrap();
    }
}
