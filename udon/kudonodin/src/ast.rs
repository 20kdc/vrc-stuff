//! An AST form of OdinSerializer data.
//! To allow for cross-references, the reference node map is represented as BTreeMap<i32, OdinASTObject>.

use super::*;
use std::collections::{BTreeMap, BTreeSet, HashMap};

pub type OdinASTType = Option<String>;

/// 'Leaf' value in the AST (or internal reference).
#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub enum OdinASTValue {
    /// Internal reference.
    /// The first encounter of this will be replaced with the appropriate OdinASTObject.
    InternalRef(i32),
    ExternalRefIdx(i32),
    Primitive(OdinPrimitive),
    Struct(OdinASTStruct),
}

impl From<&str> for OdinASTValue {
    fn from(value: &str) -> Self {
        Self::Primitive(OdinPrimitive::String(value.into()))
    }
}
impl From<OdinPrimitive> for OdinASTValue {
    fn from(value: OdinPrimitive) -> Self {
        Self::Primitive(value)
    }
}
impl From<OdinASTStruct> for OdinASTValue {
    fn from(value: OdinASTStruct) -> Self {
        Self::Struct(value)
    }
}

impl OdinASTValue {
    /// Remaps internal and external references.
    /// If an unexpected internal reference appears, the source ID is returned. (The structure will be left half-transformed.)
    /// Also adds a fixed number to external indexed references.
    /// If the internal reference map is missing, then those are not remapped and errors are impossile.
    pub fn remap_refs(&mut self, map: Option<&HashMap<i32, i32>>, extbump: i32) -> Result<(), i32> {
        match self {
            OdinASTValue::InternalRef(v) => {
                if let Some(map) = map {
                    if let Some(v2) = map.get(v) {
                        *v = *v2;
                        Ok(())
                    } else {
                        Err(*v)
                    }
                } else {
                    Ok(())
                }
            }
            OdinASTValue::ExternalRefIdx(idx) => {
                *idx += extbump;
                Ok(())
            }
            OdinASTValue::Struct(content) => {
                for v in &mut content.1 {
                    v.remap_refs(map, extbump)?;
                }
                Ok(())
            }
            OdinASTValue::Primitive(_) => Ok(()),
        }
    }

    /// Marks refs reachable from this OdinASTValue.
    pub fn mark_reachable(&self, reachable: &mut BTreeSet<i32>, queue: &mut Vec<i32>) {
        match self {
            Self::InternalRef(i) => {
                if reachable.insert(*i) {
                    queue.push(*i);
                }
            }
            Self::ExternalRefIdx(_) => {}
            Self::Primitive(_) => {}
            Self::Struct(content) => {
                for v in &content.1 {
                    v.mark_reachable(reachable, queue);
                }
            }
        }
    }
}

/// Entry in the AST.
#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub enum OdinASTEntry {
    Value(Option<String>, OdinASTValue),
    PrimitiveArray(OdinPrimitiveArray),
    /// Due to `SerializableFormatter`, the array length is not inferrable information.
    Array(i64, Vec<OdinASTEntry>),
}

/// Struct in the AST.
/// This is shared between reference types and value types; context decides which.
#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub struct OdinASTStruct(pub OdinASTType, pub Vec<OdinASTEntry>);

impl OdinASTStruct {
    /// Returns the entry list if and only if the type signature matches up.
    /// If count is 0, count is ignored.
    pub fn unwrap_fixed_type(&self, t: &str, count: usize) -> Result<&[OdinASTEntry], String> {
        if let Some(v) = &self.0 {
            if v.eq(t) {
                if count == 0 || count == self.1.len() {
                    Ok(&self.1)
                } else {
                    Err(format!(
                        "{}: expected {} subentries, got {}",
                        t,
                        count,
                        self.1.len()
                    ))
                }
            } else {
                Err(format!("{}: got type {}", t, v))
            }
        } else {
            Err(format!("{}: type expected but didn't get one", t))
        }
    }
    /// Unwraps an ISerializable. Type check is skipped, and the array dance is skipped too.
    pub fn unwrap_iserializable(&self) -> Result<&[OdinASTEntry], String> {
        if self.1.len() == 1 {
            if let OdinASTEntry::Array(_, content) = &self.1[0] {
                Ok(content)
            } else {
                Err("ISerializable: expected Array".to_string())
            }
        } else {
            Err("ISerializable: expected one entry".to_string())
        }
    }
}

pub type OdinASTRefMap = BTreeMap<i32, OdinASTStruct>;

/// This state machine converts [OdinEntry] to entries.
#[derive(Clone, Default)]
pub enum OdinASTFromEntriesStateMachine {
    #[default]
    Root,
    /// Inside OdinASTEntry::Value(OdinASTValue::Struct).
    /// Upon return, add it to the parent.
    Struct(
        Box<OdinASTFromEntriesStateMachine>,
        Option<String>,
        OdinASTStruct,
    ),
    /// Inside a reference node.
    /// Upon return, add it to the parent as an internal reference.
    /// It's probably theoretically possible for an OdinSerializer file to exist without node IDs for every referenced object.
    /// However, the serializer can't prove an object won't be referenced, so this never happens, and we don't handle it.
    RefNode(
        Box<OdinASTFromEntriesStateMachine>,
        Option<String>,
        i32,
        OdinASTStruct,
    ),
    /// Inside OdinASTEntry::Array.
    /// Upon return, add it to the parent.
    Array(Box<OdinASTFromEntriesStateMachine>, i64, Vec<OdinASTEntry>),
}

/// Contains state that must be held between reading different Odin serializations.
#[derive(Clone, Default)]
pub struct OdinASTReadContext {
    pub refs: OdinASTRefMap,
    pub types: BTreeMap<i32, String>,
}

impl OdinASTReadContext {
    /// Registers [OdinTypeEntry] and converts to [OdinASTType] form.
    pub fn handle_type(&mut self, te: OdinTypeEntry) -> OdinASTType {
        match te {
            OdinTypeEntry::Null => None,
            OdinTypeEntry::TypeName(id, name) => {
                self.types.insert(id, name.clone());
                Some(name)
            }
            OdinTypeEntry::TypeID(id) => self.types.get(&id).map(|v| v.clone()),
        }
    }
}

/// This struct converts input [OdinEntry] to an output [OdinASTEntry] hopper.
#[derive(Clone, Default)]
pub struct OdinASTFromEntries {
    pub root: Vec<OdinASTEntry>,
    pub context: OdinASTReadContext,
    pub state: OdinASTFromEntriesStateMachine,
}

impl OdinASTFromEntries {
    /// Adds an AST entry to the current state.
    pub fn add_ast(&mut self, val: OdinASTEntry) {
        match &mut self.state {
            OdinASTFromEntriesStateMachine::Root => {
                self.root.push(val);
            }
            OdinASTFromEntriesStateMachine::Struct(_, _, st) => {
                st.1.push(val);
            }
            OdinASTFromEntriesStateMachine::RefNode(_, _, _, st) => {
                st.1.push(val);
            }
            OdinASTFromEntriesStateMachine::Array(_, _, ar) => {
                ar.push(val);
            }
        }
    }

    /// Adds an [OdinEntry], advancing the state machine.
    /// The destruction of the OdinEntry is to reduce load moving around primitive arrays, etc.
    /// This is represented as a self-to-self transfer for ease of de/reconstruction.
    /// End of stream, along with various errors, are quietly ignored or nulls inserted.
    /// As a defensive measure, internal refs are verified to exist when encountered; they are nulled if not.
    /// (No defense is made against overwriting internal refs, since this is basically the same as writing an invalid type in the first place.)
    pub fn add(mut self, val: OdinEntry) -> Self {
        match val {
            OdinEntry::Value(name, info) => {
                match info {
                    OdinEntryValue::StartRefNode(te, id) => {
                        let ty = self.context.handle_type(te);
                        OdinASTFromEntries {
                            root: self.root,
                            context: self.context,
                            state: OdinASTFromEntriesStateMachine::RefNode(
                                Box::new(self.state),
                                name.clone(),
                                id,
                                OdinASTStruct(ty, Vec::new()),
                            ),
                        }
                    }
                    OdinEntryValue::StartStructNode(te) => {
                        let ty = self.context.handle_type(te);
                        OdinASTFromEntries {
                            root: self.root,
                            context: self.context,
                            state: OdinASTFromEntriesStateMachine::Struct(
                                Box::new(self.state),
                                name.clone(),
                                OdinASTStruct(ty, Vec::new()),
                            ),
                        }
                    }
                    OdinEntryValue::InternalRef(i) => {
                        if self.context.refs.contains_key(&i) {
                            self.add_ast(OdinASTEntry::Value(name, OdinASTValue::InternalRef(i)));
                        } else {
                            // defensive removal of invalid refs
                            self.add_ast(OdinASTEntry::Value(
                                name,
                                OdinASTValue::Primitive(OdinPrimitive::Null),
                            ));
                        }
                        self
                    }
                    OdinEntryValue::ExternalRefIdx(i) => {
                        self.add_ast(OdinASTEntry::Value(name, OdinASTValue::ExternalRefIdx(i)));
                        self
                    }
                    OdinEntryValue::Primitive(prim) => {
                        self.add_ast(OdinASTEntry::Value(name, OdinASTValue::Primitive(prim)));
                        self
                    }
                }
            }
            OdinEntry::PrimitiveArray(pa) => {
                self.add_ast(OdinASTEntry::PrimitiveArray(pa));
                self
            }
            OdinEntry::EndOfNode => match self.state {
                OdinASTFromEntriesStateMachine::Struct(par, nam, st) => {
                    let mut res = OdinASTFromEntries {
                        root: self.root,
                        context: self.context,
                        state: *par,
                    };
                    res.add_ast(OdinASTEntry::Value(nam, OdinASTValue::Struct(st)));
                    res
                }
                OdinASTFromEntriesStateMachine::RefNode(par, nam, id, st) => {
                    let mut res = OdinASTFromEntries {
                        root: self.root,
                        context: self.context,
                        state: *par,
                    };
                    res.context.refs.insert(id, st);
                    res.add_ast(OdinASTEntry::Value(nam, OdinASTValue::InternalRef(id)));
                    res
                }
                _ => self,
            },
            OdinEntry::StartOfArray(len) => OdinASTFromEntries {
                root: self.root,
                context: self.context,
                state: OdinASTFromEntriesStateMachine::Array(Box::new(self.state), len, Vec::new()),
            },
            OdinEntry::EndOfArray => {
                if let OdinASTFromEntriesStateMachine::Array(par, len, arr) = self.state {
                    let mut res = OdinASTFromEntries {
                        root: self.root,
                        context: self.context,
                        state: *par,
                    };
                    res.add_ast(OdinASTEntry::Array(len, arr));
                    res
                } else {
                    self
                }
            }
            OdinEntry::EndOfStream => self,
        }
    }
}

/// Contains state that must be held between writing different Odin serializations.
#[derive(Clone, Default)]
pub struct OdinASTWriteContext {
    /// All reference objects that exist.
    /// Notably, objects are removed from this map once seen!
    pub refs: OdinASTRefMap,
    /// Maps type strings to indices.
    pub types: BTreeMap<String, i32>,
    /// Type ID 0 is assigned first.
    pub type_counter: i32,
}

impl OdinASTWriteContext {
    /// Converts OdinASTType to OdinTypeEntry.
    pub fn handle_type(&mut self, ast: OdinASTType) -> OdinTypeEntry {
        match ast {
            None => OdinTypeEntry::Null,
            Some(ty) => {
                if let Some(v) = self.types.get(&ty) {
                    OdinTypeEntry::TypeID(*v)
                } else {
                    let ty2 = self.type_counter;
                    self.type_counter += 1;
                    self.types.insert(ty.clone(), ty2);
                    OdinTypeEntry::TypeName(ty2, ty)
                }
            }
        }
    }
}

impl OdinASTEntry {
    /// Destructively writes the AST entry to a target vec.
    pub fn write(self, context: &mut OdinASTWriteContext, dst: &mut Vec<OdinEntry>) {
        match self {
            Self::Value(name, value) => {
                match value {
                    OdinASTValue::Primitive(prim) => {
                        dst.push(OdinEntry::Value(name, OdinEntryValue::Primitive(prim)));
                    }
                    OdinASTValue::InternalRef(iref) => {
                        let res = context.refs.remove(&iref);
                        if let Some(mut res) = res {
                            // New
                            dst.push(OdinEntry::Value(
                                name,
                                OdinEntryValue::StartRefNode(context.handle_type(res.0), iref),
                            ));
                            for v in res.1.drain(..) {
                                v.write(context, dst);
                            }
                            dst.push(OdinEntry::EndOfNode);
                        } else {
                            // Old
                            dst.push(OdinEntry::Value(name, OdinEntryValue::InternalRef(iref)));
                        }
                    }
                    OdinASTValue::ExternalRefIdx(eref) => {
                        dst.push(OdinEntry::Value(name, OdinEntryValue::ExternalRefIdx(eref)));
                    }
                    OdinASTValue::Struct(mut st) => {
                        dst.push(OdinEntry::Value(
                            name,
                            OdinEntryValue::StartStructNode(context.handle_type(st.0)),
                        ));
                        for v in st.1.drain(..) {
                            v.write(context, dst);
                        }
                        dst.push(OdinEntry::EndOfNode);
                    }
                }
            }
            Self::PrimitiveArray(pa) => {
                dst.push(OdinEntry::PrimitiveArray(pa));
            }
            Self::Array(len, mut entries) => {
                dst.push(OdinEntry::StartOfArray(len));
                for v in entries.drain(..) {
                    v.write(context, dst);
                }
                dst.push(OdinEntry::EndOfArray);
            }
        }
    }

    /// Remaps all internal references.
    /// If an unexpected internal reference appears, the source ID is returned. (The structure will be left half-transformed.)
    /// Also bumps external indexed references.
    pub fn remap_refs(&mut self, map: Option<&HashMap<i32, i32>>, extbump: i32) -> Result<(), i32> {
        match self {
            Self::Value(_, content) => content.remap_refs(map, extbump),
            Self::PrimitiveArray(_) => Ok(()),
            Self::Array(_, content) => {
                for v in content {
                    v.remap_refs(map, extbump)?;
                }
                Ok(())
            }
        }
    }

    /// Marks refs reachable from this OdinASTEntry.
    pub fn mark_reachable(&self, reachable: &mut BTreeSet<i32>, queue: &mut Vec<i32>) {
        match self {
            Self::Value(_, v) => {
                v.mark_reachable(reachable, queue);
            }
            Self::PrimitiveArray(_) => {}
            Self::Array(_, contents) => {
                for v in contents {
                    v.mark_reachable(reachable, queue);
                }
            }
        }
    }

    /// Finds a value entry by name.
    /// Useful in reasonably sensible struct-like arrangements.
    pub fn get_value_by_name<'s>(
        name: &str,
        entries: &'s [OdinASTEntry],
    ) -> Result<&'s OdinASTValue, String> {
        for v in entries {
            if let OdinASTEntry::Value(Some(vname), val) = v {
                if name.eq(vname) {
                    return Ok(val);
                }
            }
        }
        Err(format!("field '{}' missing", name))
    }

    /// Shorthand.
    pub fn uval(val: impl Into<OdinASTValue>) -> Self {
        Self::Value(None, val.into())
    }

    /// Shorthand.
    pub fn nval(name: impl Into<String>, val: impl Into<OdinASTValue>) -> Self {
        Self::Value(Some(name.into()), val.into())
    }

    /// Array with obvious length.
    pub fn array(val: Vec<OdinASTEntry>) -> Self {
        Self::Array(val.len() as i64, val)
    }
}

/// Represents an Odin file in a nice simple wrapper. Useful for Serde.
#[derive(Clone, Debug, Default, PartialOrd, PartialEq, Serialize, Deserialize)]
pub struct OdinASTFile {
    pub refs: OdinASTRefMap,
    pub root: Vec<OdinASTEntry>,
}

impl OdinASTFile {
    /// Destructively creates OdinASTFile from a list of OdinEntry values.
    pub fn from_entries(mut src: Vec<OdinEntry>) -> Self {
        let mut state = OdinASTFromEntries::default();
        for v in src.drain(..) {
            state = state.add(v);
        }
        OdinASTFile {
            refs: state.context.refs,
            root: state.root,
        }
    }

    /// Turns this file into a list of OdinEntry values.
    /// EndOfStream isn't written because we don't seem to be finding it in Udon binaries for whatever reason.
    /// You can always append it if you want it.
    pub fn to_entry_vec(mut self) -> Vec<OdinEntry> {
        let mut vec = Vec::new();
        let mut ctx = OdinASTWriteContext {
            refs: self.refs,
            types: BTreeMap::default(),
            type_counter: 0,
        };
        for v in self.root.drain(..) {
            v.write(&mut ctx, &mut vec);
        }
        vec
    }

    /// Gets the root of a single-value-root file (all files actually encountered seem to be this kind).
    pub fn get_root_value(&self) -> Option<&OdinASTValue> {
        if self.root.len() == 1 {
            if let OdinASTEntry::Value(_, val) = &self.root[0] {
                Some(val)
            } else {
                None
            }
        } else {
            None
        }
    }

    /// Compacts and validates internal refs. On failure, the object is useless, so is destroyed.
    pub fn ref_compact(mut self) -> Result<(Self, HashMap<i32, i32>), i32> {
        let mut remap: HashMap<i32, i32> = HashMap::new();
        let mut new_ids = 0;
        for k in self.refs.keys() {
            remap.insert(*k, new_ids);
            new_ids += 1;
        }
        let mut new_refs = OdinASTRefMap::new();
        while let Some(mut res) = self.refs.pop_first() {
            for v in &mut res.1.1 {
                v.remap_refs(Some(&remap), 0)?;
            }
            new_refs.insert(
                *remap
                    .get(&res.0)
                    .expect("internal consistency (remap table missing refobj)"),
                res.1,
            );
        }
        for v in &mut self.root {
            v.remap_refs(Some(&remap), 0)?;
        }
        Ok((
            Self {
                refs: new_refs,
                root: self.root,
            },
            remap,
        ))
    }
}

/// This is an 'AST insert'.
/// This is like OdinASTFile, but it's compile-guaranteed to contain a single value.
#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub struct OdinASTInsert {
    pub refs: OdinASTRefMap,
    pub root: OdinASTValue,
}

impl TryFrom<OdinASTFile> for OdinASTInsert {
    type Error = ();
    fn try_from(mut value: OdinASTFile) -> Result<Self, Self::Error> {
        if value.root.len() == 1 {
            if let OdinASTEntry::Value(_, val) = value.root.remove(0) {
                Ok(Self {
                    refs: value.refs,
                    root: val,
                })
            } else {
                Err(())
            }
        } else {
            Err(())
        }
    }
}

impl From<OdinASTInsert> for OdinASTFile {
    fn from(value: OdinASTInsert) -> Self {
        Self {
            refs: value.refs,
            root: vec![OdinASTEntry::Value(None, value.root)],
        }
    }
}

impl From<OdinASTValue> for OdinASTInsert {
    fn from(value: OdinASTValue) -> Self {
        Self {
            refs: OdinASTRefMap::new(),
            root: value,
        }
    }
}

impl From<OdinPrimitive> for OdinASTInsert {
    fn from(value: OdinPrimitive) -> Self {
        Self {
            refs: OdinASTRefMap::new(),
            root: OdinASTValue::Primitive(value),
        }
    }
}

impl OdinASTInsert {
    /// Extracts a 'clean' OdinASTInsert using a mark-and-sweep-ish algorithm.
    /// Notably, remapping is not performed.
    pub fn extract(refs: &OdinASTRefMap, val: OdinASTValue) -> Self {
        let mut reachable: BTreeSet<i32> = BTreeSet::new();
        let mut queue: Vec<i32> = Vec::new();
        val.mark_reachable(&mut reachable, &mut queue);
        while let Some(v) = queue.pop() {
            if let Some(x) = refs.get(&v) {
                for e in &x.1 {
                    e.mark_reachable(&mut reachable, &mut queue);
                }
            }
        }
        let mut res = OdinASTRefMap::new();
        for v in reachable {
            if let Some(x) = refs.get(&v) {
                res.insert(v, x.clone());
            }
        }
        Self {
            refs: res,
            root: val,
        }
    }

    /// See [OdinASTFile::ref_compact].
    pub fn ref_compact(self) -> Result<(Self, HashMap<i32, i32>), i32> {
        let f = OdinASTFile::ref_compact(OdinASTFile {
            refs: self.refs,
            root: vec![],
        })?;
        let mut root = self.root;
        root.remap_refs(Some(&f.1), 0)?;
        Ok((
            Self {
                refs: f.0.refs,
                root: root,
            },
            f.1,
        ))
    }
}

/// Utility for building an Odin AST.
#[derive(Clone, Debug, Default)]
pub struct OdinASTBuilder {
    pub file: OdinASTFile,
    /// This folds System.RuntimeType references together.
    pub runtime_type_map: HashMap<String, i32>,
    pub next_refid: i32,
    pub next_extid: i32,
}

impl OdinASTBuilder {
    /// Allocates a reference object ID.
    pub fn alloc_refid(&mut self) -> i32 {
        let id = self.next_refid;
        self.next_refid += 1;
        id
    }

    /// Allocates an external object ID.
    pub fn alloc_extid(&mut self) -> i32 {
        let id = self.next_extid;
        self.next_extid += 1;
        id
    }

    /// This handles System.RuntimeType, as it's reasonably prevalent in the target usecase.
    /// Note that where `SerializableFormatter` is concerned, this isn't necessary, types are strings before they reach Odin.
    /// I assume the .NET serialization architecture has something to do with this.
    pub fn runtime_type(&mut self, rt: &str) -> i32 {
        if let Some(v) = self.runtime_type_map.get(rt) {
            *v
        } else {
            let res = self.alloc_refid();
            self.file.refs.insert(
                res,
                OdinASTStruct(
                    Some("System.RuntimeType, mscorlib".to_string()),
                    vec![OdinASTEntry::Value(
                        None,
                        OdinASTValue::Primitive(OdinPrimitive::String(rt.to_string())),
                    )],
                ),
            );
            self.runtime_type_map.insert(rt.to_string(), res);
            res
        }
    }

    /// Includes a [OdinASTRefMap] and returns the remapping table (in case it's needed).
    /// This can be used to, for instance, allow specifying arbitrary Odin-serialized objects via RON.
    /// It's also generally useful if moving data between [OdinASTBuilder] objects.
    /// If an internal reference is missing, this fails with the internal reference number.
    /// External references are placed at `next_extid` onwards; you can adjust that number afterwards to get a clean external reference sequence.
    /// (Alternatively, if your external references have already been accounted for, you can just leave the ID at zero.)
    /// This is useful, for instance, if you have some representation of external references.
    /// (It's outside of this crate's scope, since that would pull in figuring out Unity YAML for what we're doing here.)
    pub fn include_refs(&mut self, mut refmap: OdinASTRefMap) -> Result<HashMap<i32, i32>, i32> {
        let mut remap: HashMap<i32, i32> = HashMap::new();
        for k in refmap.keys() {
            remap.insert(*k, self.alloc_refid());
        }
        while let Some(mut res) = refmap.pop_first() {
            for v in &mut res.1.1 {
                v.remap_refs(Some(&remap), self.next_extid)?;
            }
            self.file.refs.insert(
                *remap
                    .get(&res.0)
                    .expect("internal consistency (remap table missing refobj)"),
                res.1,
            );
        }
        Ok(remap)
    }

    /// Includes an [OdinASTFile] and returns its remapped root entries, along with the remapping table (in case it's needed).
    pub fn include_file(
        &mut self,
        mut file: OdinASTFile,
    ) -> Result<(Vec<OdinASTEntry>, HashMap<i32, i32>), i32> {
        let remap: HashMap<i32, i32> = self.include_refs(file.refs)?;
        for v in &mut file.root {
            v.remap_refs(Some(&remap), self.next_extid)?;
        }
        Ok((file.root, remap))
    }

    /// Includes a [OdinASTInsert] and returns its remapped value, along with the remapping table (in case it's needed).
    pub fn include_insert(
        &mut self,
        mut file: OdinASTInsert,
    ) -> Result<(OdinASTValue, HashMap<i32, i32>), i32> {
        let remap: HashMap<i32, i32> = self.include_refs(file.refs)?;
        file.root.remap_refs(Some(&remap), self.next_extid)?;
        Ok((file.root, remap))
    }
}
