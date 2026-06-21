use kudonast::UdonInt;
use std::cell::RefCell;
use std::collections::BTreeMap;
use std::rc::Rc;

/// The KU2 equate stack.
#[derive(Clone)]
pub struct KU2EquateStack {
    stack: Vec<KU2EquateLevel>,
}

impl Default for KU2EquateStack {
    fn default() -> Self {
        KU2EquateStack {
            stack: vec![KU2EquateLevel::default()],
        }
    }
}

#[derive(Clone, Default)]
pub struct KU2EquateLevel {
    pub map: BTreeMap<String, Rc<RefCell<UdonInt>>>,
    pub macro_args: Vec<UdonInt>,
}

impl KU2EquateStack {
    /// Gets top of stack.
    #[inline]
    pub fn top(&self) -> &KU2EquateLevel {
        self.stack.last().unwrap()
    }

    /// Gets top of stack mutably.
    #[inline]
    pub fn top_mut(&mut self) -> &mut KU2EquateLevel {
        self.stack.last_mut().unwrap()
    }

    /// Pushes a new frame onto the stack and returns a mutable reference.
    #[inline]
    pub fn push(&mut self) -> &mut KU2EquateLevel {
        self.stack.push_mut(self.top().clone())
    }

    #[inline]
    pub fn pop(&mut self) -> Result<(), String> {
        if self.stack.len() != 0 {
            _ = self.stack.pop();
            Ok(())
        } else {
            Err("Equate stack underflow".to_string())
        }
    }

    /// Length for sanity checking
    #[inline]
    pub fn len(&self) -> usize {
        self.stack.len()
    }

    /// Resolves the current value of an equate, if it exists.
    pub fn get<'a>(&'a self, v: &str) -> Option<core::cell::Ref<'a, UdonInt>> {
        self.stack
            .last()
            .and_then(|map| map.map.get(v))
            .map(|v| v.borrow())
    }

    /// Removes an equate from the top-of-stack.
    pub fn undef(&mut self, id: &str) {
        self.top_mut().map.remove(id);
    }

    /// Defines an equate. If 'upper' is true, acts as `equ_up`.
    pub fn write(&mut self, id: &str, v: UdonInt, upper: bool) {
        if upper {
            // try overwrite upwards
            if let Some(target) = self.top().map.get(id) {
                target.replace(v);
                return;
            }
        }
        self.top_mut()
            .map
            .insert(id.to_string(), Rc::new(RefCell::new(v)));
    }
}
