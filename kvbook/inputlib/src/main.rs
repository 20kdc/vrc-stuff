fn main() {
    let mut a = std::env::args();
    _ = a.next();
    let src = a.next().expect("expected src");
    let dst = a.next().expect("expected dst");
    assert!(a.next().is_none());
    let pages = inputlib::read(
        &src,
        inputlib::LAYOUT_A5_W,
        inputlib::LAYOUT_A5_H,
        inputlib::LAYOUT_A5_EM,
    )
    .unwrap();
    println!("{} pages", pages.page_count());
    for i in 0..pages.page_count() {
        println!(" {}", i);
        std::fs::write(format!("{}/{}.svg", dst, i), pages.page_to_svg(i)).unwrap();
    }
}
