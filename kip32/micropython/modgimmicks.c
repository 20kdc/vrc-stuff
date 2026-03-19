#include <kip32_udon.h>
#include "py/builtin.h"
#include "py/compile.h"
#include "py/runtime.h"

KIP32_UDON_DECL_VAR(raisebat_temp1, "raisebat_temp1");
KIP32_UDON_DECL_VAR(raisebat_target, "raisebat_target");
KIP32_UDON_DECL_VAR(raisebat_ub_type, "C(type(\"VRCUdonCommonInterfacesIUdonEventReceiver\"))");
KIP32_UDON_DECL_VAR(coffee_invoke, "C(string(\"_summon_coffee\"))");

/*
 * This is responsible for the Coffee() easter egg.
 * By reading this file, you've spoiled it for yourself!
 */

static mp_obj_t coffee_make_new(const mp_obj_type_t * type_in, size_t n_args, size_t n_kw, const mp_obj_t * args);

MP_DEFINE_CONST_OBJ_TYPE(
	mp_udongimmicks_type_Coffee,
	MP_QSTR_Coffee,
	MP_TYPE_FLAG_NONE,
	make_new, coffee_make_new
	);
static mp_obj_t coffee_make_new(const mp_obj_type_t * type_in, size_t n_args, size_t n_kw, const mp_obj_t * args) {
	KIP32_UDON_EXTERN3("UnityEngineTransform.__GetComponent__SystemType__UnityEngineComponent", kip32_udon_push_raisebat_target(), kip32_udon_push_raisebat_ub_type(), kip32_udon_push_raisebat_temp1());
	KIP32_UDON_EXTERN2("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid", kip32_udon_push_raisebat_temp1(), kip32_udon_push_coffee_invoke());
	return mp_obj_malloc(mp_obj_base_t, type_in);
}
