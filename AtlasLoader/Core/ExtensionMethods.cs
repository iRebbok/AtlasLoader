using System;
using System.Collections.Generic;
using System.Text;


namespace AtlasLoader
{
    /// <summary>
    ///     Collection of extension methods used by Atlas.
    /// </summary>
    public static class ExtensionMethods
    {
        private static TimeSpan TryAddMetric(TimeSpan timeSpan, StringBuilder builder, TimeSpan duration, char symbol)
        {
            double n = Math.Floor(timeSpan.TotalDays / duration.TotalDays);
            if (n > 0)
            {
                timeSpan = timeSpan.Subtract(TimeSpan.FromDays(duration.TotalDays * n));

                builder.Append(n);
                builder.Append(symbol);
            }

            return timeSpan;
        }

        private static bool TryBufferToDouble(Queue<char> buffer, out double value)
        {
            if (double.TryParse(new string(buffer.ToArray()), out value))
            {
                buffer.Clear();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Duplicates an <see cref="IEnumerable{T}" /> to ensure the original enumerable is not altered (likely a list or array).
        /// </summary>
        /// <param name="enumerable">Enumerable to duplicate.</param>
        /// <typeparam name="T">Type of the enumerable to duplicate.</typeparam>
        /// <exception cref="ArgumentNullException"><paramref name="enumerable" /> is <see langword="null" />.</exception>
        public static IEnumerable<T> Duplicate<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }

            foreach (T item in enumerable)
            {
                yield return item;
            }
        }

        /// <summary>
        ///     Compares the equality of a <see cref="string" /> and <see cref="char" />.
        ///     I.e. whether or not <paramref name="a" /> is a single-length <see cref="string" /> containing <paramref name="b" />.
        /// </summary>
        /// <param name="a">The <see cref="string" /> to compare.</param>
        /// <param name="b">The <see cref="char" /> to compare.</param>
        /// <returns>Whether or not <paramref name="a" /> is a single-length <see cref="string" /> containing <paramref name="b" />.</returns>
        public static bool EqualsChar(this string a, char b)
        {
            return a.Length == 1 && a[0] == b;
        }

        /// <summary>
        ///     Compares the equality of a <see cref="char" /> and <see cref="string" />.
        ///     I.e. whether or not <paramref name="b" /> is a single-length <see cref="string" /> containing <paramref name="a" />.
        /// </summary>
        /// <param name="a">The <see cref="char" /> to compare.</param>
        /// <param name="b">The <see cref="string" /> to compare.</param>
        /// <returns>Whether or not <paramref name="b" /> is a single-length <see cref="string" /> containing <paramref name="a" />.</returns>
        public static bool EqualsString(this char a, string b)
        {
            return EqualsChar(b, a);
        }

        /// <summary>
        ///     Determines if a type is derived from a generic type definition.
        /// </summary>
        /// <param name="genericDefinition">The generic definition (base type).</param>
        /// <param name="derived">The type to check if it is derived.</param>
        /// <returns>Whether or not the derived type is assignable from the generic type definition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="genericDefinition" /> or <paramref name="derived" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">The definition provided was not generic.</exception>
        public static bool IsAssignableFromGeneric(this Type genericDefinition, Type derived)
        {
            if (genericDefinition == null)
            {
                throw new ArgumentNullException(nameof(genericDefinition));
            }

            if (derived == null)
            {
                throw new ArgumentNullException(nameof(derived));
            }

            if (!genericDefinition.IsGenericTypeDefinition)
            {
                throw new ArgumentException("The definition provided was not generic.", nameof(genericDefinition));
            }

            while (derived != null)
            {
                if (derived.IsGenericType && derived.GetGenericTypeDefinition() == genericDefinition)
                {
                    return true;
                }

                derived = derived.BaseType;
            }

            return false;
        }

        /// <summary>
        ///     Converts a <see cref="TimeSpan" /> to a simple timespan.
        /// </summary>
        /// <param name="timeSpan">The <see cref="TimeSpan" /> to convert.</param>
        /// <returns>The simple timespan of the <see cref="TimeSpan" />.</returns>
        public static string ToSimpleString(this TimeSpan timeSpan)
        {
            StringBuilder builder = new StringBuilder();

            timeSpan = TryAddMetric(timeSpan, builder, TimeSpan.FromDays(365), 'y');
            timeSpan = TryAddMetric(timeSpan, builder, TimeSpan.FromDays(365d / 12), 'M');
            timeSpan = TryAddMetric(timeSpan, builder, TimeSpan.FromDays(7), 'w');
            timeSpan = TryAddMetric(timeSpan, builder, TimeSpan.FromDays(1), 'd');
            timeSpan = TryAddMetric(timeSpan, builder, TimeSpan.FromHours(1), 'h');
            timeSpan = TryAddMetric(timeSpan, builder, TimeSpan.FromMinutes(1), 'm');
            TryAddMetric(timeSpan, builder, TimeSpan.FromSeconds(1), 's');

            return builder.ToString();
        }

        /// <summary>
        ///     Converts a simple timespan into a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="time">The simple timespan to convert.</param>
        /// <returns>The value of the simple timespan.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="time" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="time" /> is an invalid format.</exception>
        public static TimeSpan ToTimeSpan(this string time)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            if (!TryToTimeSpan(time, out TimeSpan value))
            {
                throw new ArgumentException("Invalid time format.", nameof(time));
            }

            return value;
        }

        /// <summary>
        ///     Attempts to convert a simple timespan into a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="time">The simple timespan to convert.</param>
        /// <param name="value">The value of the simple timespan.</param>
        /// <returns>Whether or not the simple timespan was converted.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="time" /> is <see langword="null" />.</exception>
        public static bool TryToTimeSpan(this string time, out TimeSpan value)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            Queue<char> buffer = new Queue<char>();
            value = TimeSpan.Zero;

            foreach (char c in time)
            {
                switch (c)
                {
                    case 'y':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromDays(365 * factor);
                            break;
                        }

                    case 'M':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromDays(365d / 12 * factor);
                            break;
                        }

                    case 'w':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromDays(7 * factor);
                            break;
                        }

                    case 'd':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromDays(factor);
                            break;
                        }

                    case 'h':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromHours(factor);
                            break;
                        }

                    case 'm':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromMinutes(factor);
                            break;
                        }

                    case 's':
                        {
                            if (!TryBufferToDouble(buffer, out double factor))
                            {
                                return false;
                            }

                            value += TimeSpan.FromSeconds(factor);
                            break;
                        }

                    default:
                        {
                            buffer.Enqueue(c);
                            break;
                        }
                }
            }

            return buffer.Count == 0;
        }
    }
}
