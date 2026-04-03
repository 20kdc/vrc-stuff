use proc_macro::{Group, Ident, Punct, Span, TokenStream, TokenTree};

/// Links into the `.kip32_export` section, and either sets `no_mangle` or `export_name`.
/// Can be written as one of:
/// `#[kip32_export]`: Uses `no_mangle`.
/// `#[kip32_export("example")]`: Uses `export_name`.
#[proc_macro_attribute]
pub fn kip32_export(ts: TokenStream, item: TokenStream) -> TokenStream {
    let mut total = TokenStream::new();
    let attr: TokenStream = "#[unsafe(link_section = \".kip32_export\")]"
        .parse()
        .unwrap();
    total.extend(attr);
    if ts.is_empty() {
        let attr: TokenStream = "#[unsafe(no_mangle)]".parse().unwrap();
        total.extend(attr);
    } else {
        // #[unsafe(export_name = "")]
        let mut export_name_core: TokenStream = TokenStream::new();
        export_name_core.extend([TokenTree::Ident(Ident::new(
            "export_name",
            Span::call_site(),
        ))]);
        export_name_core.extend([TokenTree::Punct(Punct::new(
            '=',
            proc_macro::Spacing::Alone,
        ))]);
        export_name_core.extend(ts);
        let mut attr_innards: TokenStream = TokenStream::new();
        attr_innards.extend([TokenTree::Ident(Ident::new("unsafe", Span::call_site()))]);
        attr_innards.extend([TokenTree::Group(Group::new(
            proc_macro::Delimiter::Parenthesis,
            export_name_core,
        ))]);
        total.extend([TokenTree::Punct(Punct::new(
            '#',
            proc_macro::Spacing::Alone,
        ))]);
        total.extend([TokenTree::Group(Group::new(
            proc_macro::Delimiter::Bracket,
            attr_innards,
        ))]);
    }
    total.extend(item);
    total
}
