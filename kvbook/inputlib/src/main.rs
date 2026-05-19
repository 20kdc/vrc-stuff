fn main() {
    let mut a = std::env::args();
    _ = a.next();
    let src = a.next().expect("expected src");
    let dst = a.next().expect("expected dst");
    assert!(a.next().is_none());
    let pages = inputlib::read(
        &src,
        &inputlib::InputOpts {
            mupdf_w: inputlib::LAYOUT_A5_W,
            mupdf_h: inputlib::LAYOUT_A5_H,
            mupdf_em: inputlib::LAYOUT_A5_EM,
        },
    )
    .unwrap();
    for page in pages.enumerate() {
        println!(" {}", page.0);
        std::fs::write(format!("{}/{}.svg", dst, page.0), page.1).unwrap();
    }
}
