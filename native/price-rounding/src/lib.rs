use std::ffi::CStr;
use std::os::raw::c_char;
use std::str::FromStr;

use rust_decimal::Decimal;
use rust_decimal::RoundingStrategy;

/// nopCommerce `RoundingType` values. The integers are the C# enum.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum RoundingType {
    Rounding001 = 0,
    Rounding005Up = 10,
    Rounding005Down = 20,
    Rounding01Up = 30,
    Rounding01Down = 40,
    Rounding05 = 50,
    Rounding1 = 60,
    Rounding1Up = 70,
}

impl RoundingType {
    fn from_id(id: i32) -> Option<Self> {
        match id {
            0 => Some(Self::Rounding001),
            10 => Some(Self::Rounding005Up),
            20 => Some(Self::Rounding005Down),
            30 => Some(Self::Rounding01Up),
            40 => Some(Self::Rounding01Down),
            50 => Some(Self::Rounding05),
            60 => Some(Self::Rounding1),
            70 => Some(Self::Rounding1Up),
            _ => None,
        }
    }

    #[cfg(test)]
    fn from_name(name: &str) -> Option<Self> {
        match name {
            "Rounding001" => Some(Self::Rounding001),
            "Rounding005Up" => Some(Self::Rounding005Up),
            "Rounding005Down" => Some(Self::Rounding005Down),
            "Rounding01Up" => Some(Self::Rounding01Up),
            "Rounding01Down" => Some(Self::Rounding01Down),
            "Rounding05" => Some(Self::Rounding05),
            "Rounding1" => Some(Self::Rounding1),
            "Rounding1Up" => Some(Self::Rounding1Up),
            _ => None,
        }
    }
}

/// Port of `PriceCalculationService.Round`. `Math.Round(value, 2)` is banker's rounding.
fn round(value: Decimal, rounding_type: RoundingType) -> Decimal {
    let ten = Decimal::from(10);
    let hundred = Decimal::from(100);

    let mut rez = value.round_dp_with_strategy(2, RoundingStrategy::MidpointNearestEven);
    let mut fraction_part = (rez - rez.trunc()) * ten;

    if fraction_part.is_zero() {
        return rez;
    }

    match rounding_type {
        RoundingType::Rounding005Up | RoundingType::Rounding005Down => {
            fraction_part = (fraction_part - fraction_part.trunc()) * ten;
            fraction_part %= Decimal::from(5);
            if fraction_part.is_zero() {
                return rez;
            }

            if rounding_type == RoundingType::Rounding005Up {
                fraction_part = Decimal::from(5) - fraction_part;
            } else {
                fraction_part = -fraction_part;
            }

            rez += fraction_part / hundred;
        }
        RoundingType::Rounding01Up | RoundingType::Rounding01Down => {
            fraction_part = (fraction_part - fraction_part.trunc()) * ten;

            if rounding_type == RoundingType::Rounding01Down && fraction_part == Decimal::from(5) {
                fraction_part = Decimal::from(-5);
            } else {
                fraction_part = if fraction_part < Decimal::from(5) {
                    -fraction_part
                } else {
                    Decimal::from(10) - fraction_part
                };
            }

            rez += fraction_part / hundred;
        }
        RoundingType::Rounding05 => {
            fraction_part *= ten;
            // Same ternary as the C# body, including the redundant `< 50` compare.
            fraction_part = if fraction_part < Decimal::from(25) {
                -fraction_part
            } else if fraction_part < Decimal::from(50) || fraction_part < Decimal::from(75) {
                Decimal::from(50) - fraction_part
            } else {
                Decimal::from(100) - fraction_part
            };

            rez += fraction_part / hundred;
        }
        RoundingType::Rounding1 | RoundingType::Rounding1Up => {
            fraction_part *= ten;

            if rounding_type == RoundingType::Rounding1Up && fraction_part > Decimal::ZERO {
                rez = rez.trunc() + Decimal::ONE;
            } else {
                rez = if fraction_part < Decimal::from(50) {
                    rez.trunc()
                } else {
                    rez.trunc() + Decimal::ONE
                };
            }
        }
        RoundingType::Rounding001 => {}
    }

    rez
}

/// `price_rounding_round` status: 0 success, 1 bad pointers, 2 amount is not a decimal, 3 unknown rounding type, 4 buffer too small.
#[no_mangle]
pub extern "C" fn price_rounding_round(
    amount: *const c_char,
    rounding_type: i32,
    result: *mut c_char,
    result_cap: i32,
) -> i32 {
    if amount.is_null() || result.is_null() || result_cap < 2 {
        return 1;
    }

    let amount = unsafe { CStr::from_ptr(amount) };
    let Ok(amount) = amount.to_str() else {
        return 2;
    };
    let Ok(value) = Decimal::from_str(amount.trim()) else {
        return 2;
    };
    let Some(rounding_type) = RoundingType::from_id(rounding_type) else {
        return 3;
    };

    let text = round(value, rounding_type).to_string();
    let bytes = text.as_bytes();
    if bytes.len() + 1 > result_cap as usize {
        return 4;
    }

    unsafe {
        std::ptr::copy_nonoverlapping(bytes.as_ptr(), result.cast::<u8>(), bytes.len());
        *result.add(bytes.len()) = 0;
    }
    0
}

#[cfg(test)]
mod tests {
    use std::ffi::CString;

    use super::*;

    #[test]
    fn matches_price_rounding_oracle() {
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join(
            "../../src/Tests/Nop.Tests/Nop.Services.Tests/Catalog/price-rounding-oracle.json",
        );
        let text = std::fs::read_to_string(&path)
            .unwrap_or_else(|error| panic!("read {}: {error}", path.display()));
        let rows: Vec<serde_json::Value> = serde_json::from_str(&text).unwrap();
        assert!(!rows.is_empty());

        for row in rows {
            let value = row["value"].as_str().unwrap();
            let rounding_name = row["roundingType"].as_str().unwrap();
            let expected = row["expected"].as_str().unwrap();
            let rounding_type = RoundingType::from_name(rounding_name)
                .unwrap_or_else(|| panic!("unknown rounding type {rounding_name}"));
            let actual = round(
                Decimal::from_str(value).unwrap(),
                rounding_type,
            );
            assert_eq!(
                actual,
                Decimal::from_str(expected).unwrap(),
                "{value} / {rounding_name}"
            );
        }
    }

    #[test]
    fn abi_writes_the_rounded_decimal() {
        let amount = CString::new("12.125").unwrap();
        let mut buffer = [0u8; 64];
        let status = price_rounding_round(
            amount.as_ptr(),
            0,
            buffer.as_mut_ptr().cast(),
            buffer.len() as i32,
        );
        assert_eq!(status, 0);
        let text = unsafe { CStr::from_ptr(buffer.as_ptr().cast()) }
            .to_str()
            .unwrap();
        let expected = round(Decimal::from_str("12.125").unwrap(), RoundingType::Rounding001);
        assert_eq!(Decimal::from_str(text).unwrap(), expected);
    }

    #[test]
    fn abi_rejects_a_non_decimal_and_an_unknown_type() {
        let amount = CString::new("nope").unwrap();
        let mut buffer = [0u8; 64];
        let status = price_rounding_round(
            amount.as_ptr(),
            0,
            buffer.as_mut_ptr().cast(),
            buffer.len() as i32,
        );
        assert_ne!(status, 0);

        let amount = CString::new("1.00").unwrap();
        let status = price_rounding_round(
            amount.as_ptr(),
            99,
            buffer.as_mut_ptr().cast(),
            buffer.len() as i32,
        );
        assert_ne!(status, 0);
    }
}
