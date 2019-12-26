using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace System.Numerics {
	/// <summary>
	/// Supports arbitrarily long decimal numbers, with a ushort scale.
	/// This means it can supports any arbitrarily big number (as far as memory can hold it), with up to 65,535 digits after the decimal period.
	/// 
	/// The smallest representable number is 1E-65535
	/// 
	/// No underflow exceptions will occur. Data beyond the 65535th digit will be lost.
	/// </summary>
	public struct BigDecimal {
		private readonly BigInteger value;
		private readonly ushort scale;

		public BigDecimal(float value) {
			string str = value.ToString("R"); // "R" format string: Round-trip - A string that can round-trip to an identical number.
			this = Parse(str); // can assing to this because it's a struct
		}

		public BigDecimal(double value) {
			string str = value.ToString("R"); // "R" format string: Round-trip - A string that can round-trip to an identical number.
			this = Parse(str); // can assing to this because it's a struct
		}

		public BigDecimal(decimal value) {
			string str = value.ToString();
			this = Parse(str); // can assing to this because it's a struct
		}

		/// <summary>
		/// Initializes a BigDecimal with an integer value.
		/// </summary>
		public BigDecimal(long value)
			: this((BigInteger)value) {
			// this constructor is provided to prevent implicit casts from choosing casting to decimal types, which are slower
		}

		/// <summary>
		/// Initializes a BigDecimal with an integer value.
		/// </summary>
		public BigDecimal(ulong value)
			: this((BigInteger)value) {
			// this constructor is provided to prevent implicit casts from choosing casting to decimal types, which are slower
		}

		/// <summary>
		/// Initializes a BigDecimal with an integer value.
		/// </summary>
		public BigDecimal(BigInteger value)
			: this(value, 0) {
		}

		/// <summary>
		/// Initializes a BigDecimal with an integer value, and a scale.
		/// 
		/// The actual decimal value will be (value * 10^(-scale)).
		/// </summary>
		public BigDecimal(BigInteger value, ushort scale) {
			this.value = value;
			this.scale = scale;
		}

		/// <summary>
		/// Parses a (big) decimal from a string.
		/// </summary>
		/// <remarks>
		/// Number format: (given in pseudo-regex, no whitespace allowed)
		/// {+|-}? [0-9]* (\. [0-9]*)? (E {+|-}? [0-9]+)?
		/// All of the parts are optional, but there must be at least one digit, and if the 'E' (case insensitive) exists there must be at least one digit before and at least one digit after the E.
		/// 
		/// Two passes are made over the string; one to validate and split it into its parts, and one to actually parts the different parts.
		/// 
		/// The exponent part is limited to the int range; an overflow will be thrown for numbers above or below that range.
		/// The digits after the decimal point are limited to the ushort range, i.e. 65535 digits. An overflow will be thrown for more digits.
		/// 
		/// If (the nubmer of digits after the decimal point - the exponent) is less than -ushort.MAX_VALUE, then precision will be lost. (e.g. 1E-70000 == 0, and 123.456E-65536 == 120E65536)
		/// </remarks>
		/// <exception cref="ArgumentNullException">The given string is null</exception>
		/// <exception cref="FormatException">The number could not be parsed because it had a bad format or invalid characters</exception>
		/// <exception cref="OverflowException">One of the parts was over its limit: the exponent or the number of digits after the decimal point</exception>
		public static BigDecimal Parse(string str) {
			if (str == null)
				throw new ArgumentNullException("str", "BigDecimal.Parse: Cannot parse null");

			// first, we go over the string, separate it into parts.
			// At this stage, we create:
			//		valueBuilder: Contains a string that represents the value (should be parsed using BigInteger), e.g. for 1234.5678E-17, will contain "12345678".
			//		exponentBuilder: Is null if no exponent exists in the original string, or contains a string that represents that exponent otherwise, e.g. for 1234.5678E-17, will contain "-17".
			//		scale: Contains the scale from the original number (before the exponent was applied), e.g. for 1234.5678E-17, will contain 4.
			// The second stage should parse the results. In our example of 1234.5678E-17, the final result should be value=12345678 and scale=4-(-17)=21.
			ushort scale = 0;
			StringBuilder valueBuilder = new StringBuilder();
			StringBuilder exponentBuilder = null;


			ParseState state = ParseState.Start;

			// non-trivial things that are using in multiple cases
			Action<char> formatException = c => { throw new FormatException("BigDecimal.Parse: invalid character '" + c + "' in: " + str); };
			Action startExponent = () => { exponentBuilder = new StringBuilder(); state = ParseState.E; };
			foreach (char c in str) {
				switch (state) {
					case ParseState.Start:
						if (char.IsDigit(c) || c == '-' || c == '+') {
							state = ParseState.Integer;
							valueBuilder.Append(c);
						} else if (c == '.') {
							state = ParseState.Decimal;
						} else {
							formatException(c);
						}
						break;

					case ParseState.Integer:
						if (char.IsDigit(c)) {
							valueBuilder.Append(c);
						} else if (c == '.') {
							state = ParseState.Decimal;
						} else if (c == 'e' || c == 'E') {
							startExponent();
						} else {
							formatException(c);
						}
						break;

					case ParseState.Decimal:
						if (char.IsDigit(c)) {
							// checked so that an overflow is thrown for too much precision
							checked { scale++; }
							valueBuilder.Append(c);
						} else if (c == 'e' || c == 'E') {
							startExponent();
						} else {
							formatException(c);
						}
						break;

					case ParseState.E:
						if (char.IsDigit(c) || c == '-' || c == '+') {
							state = ParseState.Exponent;
							exponentBuilder.Append(c);
						} else {
							formatException(c);
						}
						break;

					case ParseState.Exponent:
						if (char.IsDigit(c)) {
							exponentBuilder.Append(c);
						} else {
							formatException(c);
						}
						break;
				}
			}

			if (valueBuilder.Length == 0 ||
				(valueBuilder.Length == 1 && !char.IsDigit(valueBuilder[0]))) {
				// the value doesn't have any digit (one character could be the sign)
				throw new FormatException("BigDecimal.Parse: string didn't contain a value: \"" + str + "\"");
			}

			if (exponentBuilder != null &&
				(exponentBuilder.Length == 0 ||
				(valueBuilder.Length == 1 && !char.IsDigit(valueBuilder[0])))) {
				// the scale builder exists but is empty, meaning there was an 'e' in the number, but no digits afterwards (one character could be the sign)
				throw new FormatException("BigDecimal.Parse: string contained an 'E' but no exponent value: \"" + str + "\"");
			}

			BigInteger value = BigInteger.Parse(valueBuilder.ToString());

			if (exponentBuilder == null) {
				// simple case with no exponent
			} else {

				// we need to correct the scale to match the given exponent
				// Note: The scale goes downwards (i.e. a large scale means more precision) while the exponent goes up (i.e. a large exponent means the number is larger)
				int exponent = int.Parse(exponentBuilder.ToString());
				if (exponent > 0) {
					if (exponent <= scale) {
						// relatively simply case; decrease the scale by the exponent (e.g. 1.2e1 would have a scale of 1 and exponent of 1 resulting in 12 with scale of 0)
						scale -= (ushort)exponent;
					} else {
						// scale would be negative; increase the actual value to represent that (remember, scale is only used for places after the decimal point)
						exponent -= scale;
						scale = 0;
						value *= BigInteger.Pow(10, exponent);
					}
				} else if (exponent < 0) {
					exponent = (-exponent) + scale;
					if (exponent <= ushort.MaxValue) {
						// agian, relatively simple case; increate the scale by the (negated) exponent (e.g. 1.2e-1 would have a scale of 1 and an exponent of -1 resulting in 12 with scale of 2, i.e. 0.12)
						scale = (ushort)exponent;
					} else {
						// scale would overflow; lose some precision instead by dividing the value (integer truncating division)
						scale = ushort.MaxValue;
						value /= BigInteger.Pow(10, exponent - ushort.MaxValue);
					}
				}
			}

			return new BigDecimal(value, scale);
		}

		private BigDecimal Upscale(ushort newScale) {
			if (newScale < scale)
				throw new InvalidOperationException("Cannot upscale a BigDecimal to a smaller scale!");

			return new BigDecimal(value * BigInteger.Pow(10, newScale - scale), newScale);
		}

		private static ushort SameScale(ref BigDecimal left, ref BigDecimal right) {
			var newScale = Math.Max(left.scale, right.scale);
			left = left.Upscale(newScale);
			right = right.Upscale(newScale);
			return newScale;
		}

        public static bool operator ==(BigDecimal left, int right)
        {
            return left.Equals((System.Numerics.BigDecimal)right);
        }

        public static bool operator !=(BigDecimal left, int right)
        {
            return !left.Equals((System.Numerics.BigDecimal)right);
        }

        public static bool operator ==(BigDecimal left, BigDecimal right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigDecimal left, BigDecimal right)
        {
            return !left.Equals(right);
        }

        public static bool operator >(BigDecimal left, BigDecimal right)
        {
            return ((left-right).value > 0);
        }

        public static bool operator >=(BigDecimal left, BigDecimal right)
        {
            return ((left - right).value >= 0);
        }

        public static bool operator <(BigDecimal left, BigDecimal right)
        {
            return ((left - right).value < 0);
        }

        public static bool operator <=(BigDecimal left, BigDecimal right)
        {
            return ((left - right).value <= 0);
        }

        public static bool operator ==(BigDecimal left, decimal right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigDecimal left, decimal right)
        {
            return !left.Equals(right);
        }

        public static bool operator >(BigDecimal left, decimal right)
        {
            return ((left - right).value > 0);
        }

        public static bool operator >=(BigDecimal left, decimal right)
        {
            return ((left - right).value >= 0);
        }

        public static bool operator <(BigDecimal left, decimal right)
        {
            return ((left - right).value < 0);
        }

        public static bool operator <=(BigDecimal left, decimal right)
        {
            return ((left - right).value <= 0);
        }

        public static bool operator ==(decimal left, BigDecimal right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(decimal left, BigDecimal right)
        {
            return !left.Equals(right);
        }

        public static bool operator >(decimal left, BigDecimal right)
        {
            return ((left - right).value > 0);
        }

        public static bool operator >=(decimal left, BigDecimal right)
        {
            return ((left - right).value >= 0);
        }

        public static bool operator <(decimal left, BigDecimal right)
        {
            return ((left - right).value < 0);
        }

        public static bool operator <=(decimal left, BigDecimal right)
        {
            return ((left - right).value <= 0);
        }

        public static BigDecimal sqrt(BigDecimal input)
        {
            BigDecimal num =(input*input+input)/(2*input);
            for (int i=0;i<20;i++) {
                Debug.Log("ㅌㅌㅌ" + num);
                BigDecimal val = sqrt1(num, input);
                if(val!=num)
                    num = val;
                else
                {
                    return num;
                }
            }
            return num;
        }

        public static BigDecimal sqrt1(BigDecimal num,BigDecimal input)
        {
            BigDecimal output = (num * num + input) / (2 * num);
            return output;
        }


        public static BigDecimal operator +(BigDecimal left, BigDecimal right) {
			var scale = SameScale(ref left, ref right);
			return new BigDecimal(left.value + right.value, scale);
		}

		public static BigDecimal operator -(BigDecimal left, BigDecimal right) {
			var scale = SameScale(ref left, ref right);
			return new BigDecimal(left.value - right.value, scale);
		}

        public static BigDecimal operator %(BigDecimal left, BigDecimal right)
        {
            if (left == 0)
                return 0;
            var scale = SameScale(ref left, ref right);
            return new BigDecimal(left.value % right.value, scale);
        }

        public static BigDecimal operator *(BigDecimal left, BigDecimal right) {
			var value = left.value * right.value;
			var scale = (int)left.scale + (int)right.scale;
			if (scale > ushort.MaxValue) {
				value /= BigInteger.Pow(10, scale - ushort.MaxValue);
				scale = ushort.MaxValue;
			}
			return new BigDecimal(value, (ushort)scale);
		}

        public static BigDecimal operator /(BigDecimal left, BigDecimal right)
        {
            var MaxScale = Math.Max(left.scale,right.scale);
            var value = (left.value * BigInteger.Pow(10,5+(left.scale))) / (right.value* BigInteger.Pow(10,(right.scale)));
            var scale = Math.Abs(MaxScale);
            if (scale > 50)
            {
                value /= BigInteger.Pow(10, scale - 50);
                scale = 50;
            }
            return new BigDecimal(value, (ushort)(scale+5));
        }


        private enum ParseState {
			/// <summary>
			/// First character
			/// </summary>
			Start,
			/// <summary>
			/// During the first part of the number, or right after the sign
			/// </summary>
			Integer,
			/// <summary>
			/// After the decimal point, or during a number in it
			/// </summary>
			Decimal,
			/// <summary>
			/// Right after the E
			/// </summary>
			E,
			/// <summary>
			/// After the E's sign, or during its number (i.e. the exponent).
			/// </summary>
			Exponent
		}



        public static explicit operator float(BigDecimal v)
        {
            if (v == 0)
                return 0;

            var value = (v.value) / BigInteger.Pow(10, v.scale - ushort.MaxValue);
            return (float)value;
        }

        public static implicit operator BigDecimal(float value)
        {
            return new BigDecimal(value);
        }

        public static implicit operator BigDecimal(sbyte value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(byte value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(short value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(ushort value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(int value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(uint value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(long value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(ulong value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(decimal value) {
			return new BigDecimal(value);
		}

		public static implicit operator BigDecimal(BigInteger value) {
			return new BigDecimal(value);
		}

		public override string ToString() {
			if (scale == 0) {
				return value.ToString() + "."; // we add a decimal point at the end so we always know it's a decimal and not an integer.
			} else {
				// we need to add a decimal point at the right place
				string result = value.ToString();
				if (result.Length > scale) {
					// the number is big enough to add the point inside it
					return result.Insert(result.Length - scale, ".");
				} else {
					// add a leading zero and a (potentially lot of, potentially none) zeros
					return "0." + new String('0', scale - result.Length) + result;
				}
			}
		}

	}
}
