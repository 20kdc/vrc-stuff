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
        total.extend([
            TokenTree::Punct(Punct::new('#', proc_macro::Spacing::Alone)),
            TokenTree::Group(Group::new(
                proc_macro::Delimiter::Bracket,
                TokenStream::from_iter([
                    TokenTree::Ident(Ident::new("unsafe", Span::call_site())),
                    TokenTree::Group(Group::new(
                        proc_macro::Delimiter::Parenthesis,
                        TokenStream::from_iter([
                            TokenStream::from_iter([
                                TokenTree::Ident(Ident::new("export_name", Span::call_site())),
                                TokenTree::Punct(Punct::new('=', proc_macro::Spacing::Alone)),
                            ]),
                            ts,
                        ]),
                    )),
                ]),
            )),
        ]);
    }
    total.extend(item);
    total
}

#[proc_macro]
pub fn kip32_internal_udontypes(_ts: TokenStream) -> TokenStream {
    let mut res = TokenStream::new();
    for v in kudon_apijson::type_names() {
        let info = kudon_apijson::type_by_name(&v).unwrap();
        let derive_ts: TokenStream = "#[derive(Clone, Copy)]".parse().unwrap();
        res.extend(derive_ts);
        res.extend([
            TokenTree::Ident(Ident::new("pub", Span::call_site())),
            TokenTree::Ident(Ident::new("enum", Span::call_site())),
            TokenTree::Ident(Ident::new_raw(&v, Span::call_site())),
            TokenTree::Group(Group::new(proc_macro::Delimiter::Brace, TokenStream::new())),
        ]);
        res.extend([
            TokenTree::Ident(Ident::new("impl", Span::call_site())),
            TokenTree::Ident(Ident::new("UdonType", Span::call_site())),
            TokenTree::Ident(Ident::new("for", Span::call_site())),
            TokenTree::Ident(Ident::new_raw(&v, Span::call_site())),
            TokenTree::Group(Group::new(proc_macro::Delimiter::Brace, TokenStream::new())),
        ]);
        for base in kudon_apijson::type_bases_and_self(&v, info) {
            res.extend([
                TokenTree::Ident(Ident::new("impl", Span::call_site())),
                TokenTree::Ident(Ident::new("UdonCastable", Span::call_site())),
                TokenTree::Punct(Punct::new('<', proc_macro::Spacing::Alone)),
                TokenTree::Ident(Ident::new_raw(&base, Span::call_site())),
                TokenTree::Punct(Punct::new('>', proc_macro::Spacing::Alone)),
                TokenTree::Ident(Ident::new("for", Span::call_site())),
                TokenTree::Ident(Ident::new_raw(&v, Span::call_site())),
                TokenTree::Group(Group::new(proc_macro::Delimiter::Brace, TokenStream::new())),
            ]);
        }
    }
    res
}
