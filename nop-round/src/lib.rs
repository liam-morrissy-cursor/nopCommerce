use rust_decimal::Decimal;
use rust_decimal::RoundingStrategy;
use std::str::FromStr;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum RoundingType {
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
    pub fn from_name(name: &str) -> Option<Self> {
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

fn decimal_whole_part(value: Decimal) -> i64 {
    value.trunc().mantissa().try_into().unwrap_or(0)
}

pub fn round(value: Decimal, rounding_type: RoundingType) -> Decimal {
    let mut rez = value.round_dp_with_strategy(2, RoundingStrategy::MidpointNearestEven);
    let mut fraction_part = (rez - rez.trunc()) * Decimal::from(10);

    if fraction_part.is_zero() {
        return rez;
    }

    match rounding_type {
        RoundingType::Rounding005Up | RoundingType::Rounding005Down => {
            fraction_part = (fraction_part - fraction_part.trunc()) * Decimal::from(10);
            let mut cents = decimal_whole_part(fraction_part) % 5;
            if cents == 0 {
                return rez;
            }
            if rounding_type == RoundingType::Rounding005Up {
                cents = 5 - cents;
            } else {
                cents *= -1;
            }
            rez += Decimal::from(cents) / Decimal::from(100);
        }
        RoundingType::Rounding01Up | RoundingType::Rounding01Down => {
            fraction_part = (fraction_part - fraction_part.trunc()) * Decimal::from(10);
            let cents = decimal_whole_part(fraction_part);
            let adjustment = if rounding_type == RoundingType::Rounding01Down && cents == 5 {
                -5
            } else if cents < 5 {
                cents * -1
            } else {
                10 - cents
            };
            rez += Decimal::from(adjustment) / Decimal::from(100);
        }
        RoundingType::Rounding05 => {
            let fp_i = decimal_whole_part(fraction_part * Decimal::from(10));
            let adjustment = if fp_i < 25 {
                fp_i * -1
            } else if fp_i < 75 {
                50 - fp_i
            } else {
                100 - fp_i
            };
            rez += Decimal::from(adjustment) / Decimal::from(100);
        }
        RoundingType::Rounding1 | RoundingType::Rounding1Up => {
            let fp_i = decimal_whole_part(fraction_part * Decimal::from(10));
            if rounding_type == RoundingType::Rounding1Up && fp_i > 0 {
                rez = rez.trunc() + Decimal::ONE;
            } else {
                rez = if fp_i < 50 {
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

pub fn round_str(value: &str, rounding_type: RoundingType) -> Result<Decimal, String> {
    let decimal = Decimal::from_str(value).map_err(|e| e.to_string())?;
    Ok(round(decimal, rounding_type))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn d(s: &str) -> Decimal {
        Decimal::from_str(s).unwrap()
    }

    fn assert_round(value: &str, expected: &str, rounding_type: RoundingType) {
        let result = round_str(value, rounding_type).unwrap();
        assert_eq!(
            result,
            d(expected),
            "round({value}, {rounding_type:?}) expected {expected}, got {result}"
        );
    }

    #[test]
    fn rounding001_bankers_and_baseline() {
        assert_round("12.366", "12.37", RoundingType::Rounding001);
        assert_round("12.363", "12.36", RoundingType::Rounding001);
        assert_round("12.000", "12.00", RoundingType::Rounding001);
        assert_round("12.001", "12.00", RoundingType::Rounding001);
        assert_round("1.225", "1.22", RoundingType::Rounding001);
        assert_round("2.005", "2.00", RoundingType::Rounding001);
        assert_round("2.015", "2.02", RoundingType::Rounding001);
        assert_round("12.365", "12.36", RoundingType::Rounding001);
    }

    #[test]
    fn rounding005_up_and_down() {
        assert_round("12.34", "12.35", RoundingType::Rounding005Up);
        assert_round("12.36", "12.40", RoundingType::Rounding005Up);
        assert_round("12.35", "12.35", RoundingType::Rounding005Up);
        assert_round("12.34", "12.30", RoundingType::Rounding005Down);
        assert_round("12.36", "12.35", RoundingType::Rounding005Down);
        assert_round("12.35", "12.35", RoundingType::Rounding005Down);
    }

    #[test]
    fn rounding01_up_and_down() {
        assert_round("12.35", "12.40", RoundingType::Rounding01Up);
        assert_round("12.05", "12.10", RoundingType::Rounding01Up);
        assert_round("12.35", "12.30", RoundingType::Rounding01Down);
        assert_round("12.05", "12.00", RoundingType::Rounding01Down);
    }

    #[test]
    fn rounding05_and_whole_dollar() {
        assert_round("12.24", "12.00", RoundingType::Rounding05);
        assert_round("12.49", "12.50", RoundingType::Rounding05);
        assert_round("12.74", "12.50", RoundingType::Rounding05);
        assert_round("12.99", "13.00", RoundingType::Rounding05);
        assert_round("12.49", "12.00", RoundingType::Rounding1);
        assert_round("12.50", "13.00", RoundingType::Rounding1);
        assert_round("12.01", "13.00", RoundingType::Rounding1Up);
    }
}
