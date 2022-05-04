using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace test_it_sharp
{
    // implementation of the task is unreliable due using decimal nums as a base math
    // to implement strongly one have to implement unified base number toolset (base math operation, conversion as minimum)
    // which is to complex for the test
    // so the solution is in place and got to be tested with cases of usage
    // but it brings overall an idea and satisfies the task, calculating the answer and offering some debug tools

    public unsafe struct Number
    {
        public const int BASE_MIN_I = 2;
        public const int BASE_MAX_I = int.MaxValue / 3; // this + other + fracture = 3 (non binary)
        public const int SIZE_MIN_I = 1;
        public const int SIZE_MAX_I = 32;

        private fixed int _value[SIZE_MAX_I];

        public readonly int Base;
        public readonly int Size;

        public bool IsOverflow { get; private set; }

        public Number(int size, int @base = 13)
        {
            if(@base.Clamp(BASE_MIN_I, BASE_MAX_I) != @base)
            {
                throw new Exception($"illegal base value (base: {@base} range: [{BASE_MIN_I}, {BASE_MAX_I}])");
            }

            if(size.Clamp(SIZE_MIN_I, SIZE_MAX_I) != size)
            {
                throw new Exception($"illegal size value (size: {size} range: [{SIZE_MIN_I}, {SIZE_MAX_I}])");
            }

            Size = size;
            Base = @base;
            IsOverflow = false;
        }

        public override string ToString()
        {
            var separator = Base < 16 ? string.Empty : ":";
            var builder = new StringBuilder();
            for(var offset = Size - 1; offset > -1; --offset)
            {
                builder
                    .Append(this.ClampDigit(_value[offset]) != _value[offset] ? "*" : $"{_value[offset]:X1}")
                    .Append(offset == 0 ? string.Empty : separator);
            }
            return builder.ToString();
        }

        public static Number Create(int source, int size, int @base = 13)
        {
            var result = new Number(size, @base);
            var offset = -1;
            while(++offset != size)
            {
                result._value[offset] = source % @base;
                source /= @base;
            }

            result.IsOverflow = source != 0;
            return result;
        }

        public void Inc()
        {
            var offset = -1;
            IsOverflow = true;
            while(++offset < Size && IsOverflow)
            {
                _value[offset]++;
                IsOverflow = _value[offset] == Base;
                if(IsOverflow)
                {
                    _value[offset] = 0;
                }
            }
        }

        public void Dec()
        {
            // todo: test

            var offset = -1;
            IsOverflow = true;
            while(++offset < Size && IsOverflow)
            {
                _value[offset]--;
                IsOverflow = _value[offset] == -1;
                if(IsOverflow)
                {
                    _value[offset] = Base - 1;
                }
            }
        }

        public void Add(Number other)
        {
            if(Base != other.Base)
            {
                // todo: convert to home base
                throw new Exception("illegal operation, base isn't the same");
            }

            if(!other.Assert())
            {
                throw new Exception("illegal format, number register(s) is(are) out of range");
            }

            var offset = -1;
            var fracture = 0;
            while(++offset < Size)
            {
                _value[offset] += other._value[offset] + fracture;
                fracture = _value[offset] / Base;
                _value[offset] = _value[offset] % Base;
                IsOverflow = fracture != 0;
            }
        }

        public bool Assert()
        {
            for(var offset = 0; offset < Size; offset++)
            {
                if(this.ClampDigit(_value[offset]) != _value[offset])
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsNice(int slice)
        {
            if(slice > Size)
            {
                throw new Exception($"slice is too wide (slice: {slice} size: {Size})");
            }

            checked
            {
                var sumLeft = 0;
                var sumRight = 0;
                for(var offset = 0; offset < slice; offset++)
                {
                    sumLeft += _value[Size - 1 - offset];
                    sumRight += _value[offset];
                }

                return sumLeft == sumRight;
            }
        }

        public int Sum()
        {
            checked
            {
                var result = 0;
                for(var offset = 0; offset < Size; ++offset)
                {
                    result += _value[offset];
                }
                return result;
            }
        }
    }

    public class Quantity
    {
        public int Value;
    }

    public static class Extensions
    {
        public static int ClampDigit(this Number source, int digit)
        {
            return digit.Clamp(0, source.Base - 1);
        }

        public static int Clamp(this int value, int min, int max)
        {
            if(min > max)
            {
                throw new Exception($"illegal values (min: {min} max: {max})");
            }

            value = value < min ? min : value;
            value = value > max ? max : value;
            return value;
        }

        public static string ToConsole(this string source)
        {
            Console.WriteLine(source);
            return source;
        }

        public static string ToConsoleDiag(this string source)
        {
            var posX = Console.CursorTop;
            Console.Write($"{source}{new string(' ', Console.WindowWidth - source.Length)}");
            Console.SetCursorPosition(0, posX);
            return source;
        }

        public static string ToFile(this string source)
        {
            File.WriteAllText("results.txt", source);
            return source;
        }
    }

    public static class Impl
    {
        public static string MethodBrute(TimeSpan echoInterval, TimeSpan maxInterval, int size = 5, int slice = 2)
        {
            var step = TimeSpan.Zero;
            var timer = Stopwatch.StartNew();
            var result = 0;
            var number = Number.Create(0, size);

            while(!number.IsOverflow)
            {
                result += number.IsNice(slice) ? 1 : 0;
                number.Inc();

                if(timer.Elapsed - step > echoInterval)
                {
                    $"{timer.Elapsed:g}\t> {number} : {result}".ToConsoleDiag();
                    step += echoInterval;
                }

                if(timer.Elapsed > maxInterval)
                {
                    return $"max interval reached; number reached: {number}; nice nums found: {result} ({Number.Create(result, size)})";
                }
            }

            timer.Stop();
            return new StringBuilder()
                .AppendLine($"total: {result} ({Number.Create(result, Number.SIZE_MAX_I)})")
                .AppendLine($"time elapsed: {timer.ElapsedMilliseconds} ms")
                .ToString();
        }

        public static string MethodSums(TimeSpan echoInterval, TimeSpan maxInterval, int size = 5, int slice = 2)
        {
            if(slice + slice > size)
            {
                throw new Exception($"slice is too wide, not implemented for overlapped slices (slice: {slice} size: {size})");
            }

            var step = TimeSpan.Zero;
            var timer = Stopwatch.StartNew();
            var sums = new Dictionary<int, Quantity>();
            var number = Number.Create(0, slice);
            var gap = size - slice - slice;

            while(!number.IsOverflow)
            {
                var sum = number.Sum();
                if(!sums.TryGetValue(sum, out var quantity))
                {
                    quantity = new Quantity();
                    sums.Add(sum, quantity);
                }
                quantity.Value++;

                if(timer.Elapsed - step > echoInterval)
                {
                    $"{timer.Elapsed:g}\t> {number} : {sum} of sums found: {sums.Count}".ToConsoleDiag();
                    step += echoInterval;
                }

                if(timer.Elapsed > maxInterval)
                {
                    return "breaking on sums calculation";
                }

                number.Inc();
            }

            ulong result = 0;
            var numbersSlice = 0;
            var numbersInSumMin = int.MaxValue;
            var numbersInSumMax = int.MinValue;
            var gapCommutation = (ulong)Math.Pow(number.Base, gap);
            foreach(var quantity in sums.Values)
            {
                numbersSlice += quantity.Value;
                result += (ulong)quantity.Value * (ulong)quantity.Value * gapCommutation;
                numbersInSumMax = quantity.Value > numbersInSumMax ? quantity.Value : numbersInSumMax;
                numbersInSumMin = quantity.Value < numbersInSumMin ? quantity.Value : numbersInSumMin;

                if(timer.Elapsed - step > echoInterval)
                {
                    $"{timer.Elapsed:g}\t> current sum: {quantity.Value}".ToConsoleDiag();
                    step += echoInterval;
                }

                if(timer.Elapsed > maxInterval)
                {
                    return "breaking on sums gathering";
                }
            }

            timer.Stop();
            return new StringBuilder()
                .AppendLine("results:")
                .AppendLine($"unique sums: {sums.Count}")
                .AppendLine($"max numbers per sum: {numbersInSumMax}")
                .AppendLine($"min numbers per sum: {numbersInSumMin}")
                .AppendLine($"total tails: {numbersSlice} ({Number.Create(numbersSlice, Number.SIZE_MAX_I)})")
                .AppendLine($"total combinations: {result}")
                .AppendLine($"time elapsed: {timer.ElapsedMilliseconds} ms")
                .ToString();
        }
    }

    public class Program
    {
        private static void Main(string[] args)
        {
            const int SIZE_I = 13;
            const int SLICE_I = 6;
            new StringBuilder()
                .AppendLine("-- method BRUTE")
                .AppendLine(Impl.MethodBrute(TimeSpan.FromMilliseconds(100d), TimeSpan.FromSeconds(10d), SIZE_I, SLICE_I))
                .AppendLine("-- method SUMS")
                .AppendLine(Impl.MethodSums(TimeSpan.FromMilliseconds(100d), TimeSpan.FromSeconds(10d), SIZE_I, SLICE_I))
                .ToString()
                .ToConsole()
                .ToFile();
        }
    }
}
