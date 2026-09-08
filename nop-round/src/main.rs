use nop_round::{round_str, RoundingType};
use serde::{Deserialize, Serialize};
use std::io::{self, BufRead, Write};

#[derive(Debug, Deserialize)]
struct RoundRequest {
    id: u64,
    value: String,
    rounding_type: String,
}

#[derive(Debug, Serialize)]
struct RoundResponse {
    id: u64,
    #[serde(skip_serializing_if = "Option::is_none")]
    result: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}

fn main() {
    let stdin = io::stdin();
    let mut stdout = io::stdout();

    for line in stdin.lock().lines() {
        let line = match line {
            Ok(line) if !line.trim().is_empty() => line,
            Ok(_) => continue,
            Err(_) => break,
        };

        let response = match serde_json::from_str::<RoundRequest>(&line) {
            Ok(request) => handle_request(request),
            Err(err) => RoundResponse {
                id: 0,
                result: None,
                error: Some(format!("invalid request: {err}")),
            },
        };

        if let Ok(json) = serde_json::to_string(&response) {
            let _ = writeln!(stdout, "{json}");
            let _ = stdout.flush();
        }
    }
}

fn handle_request(request: RoundRequest) -> RoundResponse {
    let rounding_type = match RoundingType::from_name(&request.rounding_type) {
        Some(value) => value,
        None => {
            return RoundResponse {
                id: request.id,
                result: None,
                error: Some(format!("unknown rounding_type: {}", request.rounding_type)),
            };
        }
    };

    match round_str(&request.value, rounding_type) {
        Ok(result) => RoundResponse {
            id: request.id,
            result: Some(result.to_string()),
            error: None,
        },
        Err(err) => RoundResponse {
            id: request.id,
            result: None,
            error: Some(err),
        },
    }
}
