use super::*;
use json::JsonValue;
use serde::{Deserialize, Serialize};
use std::io::Write;

/// [UdonRawProgram] plus support information required for emitting udonjson.
/// Essentially, this is a `.udonjson` file in a single struct.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonAnnotatedRawProgram {
    pub program: UdonRawProgram,
    pub unity_obj: Vec<UdonUnityObject>,
    pub network_call_metadata: Vec<UdonNetworkCallMetadata>,
}

impl UdonAnnotatedRawProgram {
    pub fn link(program: &UdonProgram) -> Result<Self, String> {
        let mut unity_obj = Vec::new();
        let rawprogram = udonprogram_emit_udonrawprogram(program, &mut unity_obj)?;
        Ok(UdonAnnotatedRawProgram {
            program: rawprogram,
            unity_obj,
            network_call_metadata: program.network_call_metadata.clone(),
        })
    }
    /// This is for cases where a full udonjson program isn't involved at all.
    pub fn from_unannotated(program: UdonRawProgram) -> Self {
        UdonAnnotatedRawProgram {
            program,
            unity_obj: vec![],
            network_call_metadata: vec![],
        }
    }
    pub fn to_udonjson(&self) -> Result<JsonValue, String> {
        // see udonprogram_emit_odin
        let stage1_file = {
            let mut builder = OdinASTBuilder::default();
            let val = OdinSTSerializable::serialize(&self.program, &mut builder);
            builder.file.root.push(OdinASTEntry::uval(val));
            builder.file
        };

        let udon_binary = OdinEntry::write_all_to_bytes(&stage1_file.to_entry_vec());
        let final_binary = Vec::new();

        let mut gz_encoder =
            flate2::write::GzEncoder::new(final_binary, flate2::Compression::default());
        gz_encoder
            .write_all(&udon_binary)
            .map_err(|v| format!("{:?}", v))?;
        let encoded = gz_encoder.finish().map_err(|v| format!("{:?}", v))?;

        let serialized_program_compressed_bytes =
            JsonValue::Array(encoded.iter().map(|v| (*v as f64).into()).collect());

        let program_unity_engine_objects = JsonValue::Array(
            self.unity_obj
                .iter()
                .map(|v| match v {
                    UdonUnityObject::Ref(guid, file_id) => {
                        let mut res = JsonValue::new_object();
                        res["guid"] = guid.clone().into();
                        res["fileID"] = (*file_id as f64).into();
                        res
                    }
                })
                .collect(),
        );

        let mut network_calling_entrypoint_metadata = Vec::new();
        for v in &self.network_call_metadata {
            let mut res = JsonValue::new_object();
            res["_name"] = v.name.clone().into();
            let mut parameters_arr = Vec::new();
            for p in &v.parameters {
                let mut res = JsonValue::new_object();
                res["_name"] = p.0.clone().into();
                res["_type"] =
                    p.1.sync_type
                        .ok_or_else(|| {
                            format!(
                                "Network RPC {} parameter {} has no sync type",
                                &v.name, &p.0
                            )
                        })?
                        .into();
                parameters_arr.push(res);
            }
            res["_parameters"] = JsonValue::Array(parameters_arr);
            res["_maxEventsPerSecond"] = v.max_events_per_second.into();
            network_calling_entrypoint_metadata.push(res);
        }

        let mut monobehaviour = JsonValue::new_object();
        monobehaviour["serializedProgramCompressedBytes"] = serialized_program_compressed_bytes;
        monobehaviour["programUnityEngineObjects"] = program_unity_engine_objects;
        monobehaviour["networkCallingEntrypointMetadata"] =
            JsonValue::Array(network_calling_entrypoint_metadata);

        let mut outer_object = JsonValue::new_object();
        outer_object["MonoBehaviour"] = monobehaviour;
        Ok(outer_object)
    }
    pub fn from_udonjson(_json: &JsonValue) -> Result<Self, String> {
        Err(format!(
            "JSON to UdonAnnotatedRawProgram is not yet implemented."
        ))
    }
}

/// Links [UdonProgram] into .udonjson JSON (ready-to-use).
pub fn udonprogram_emit_udonjson(program: &UdonProgram) -> Result<JsonValue, String> {
    UdonAnnotatedRawProgram::link(program)?.to_udonjson()
}
