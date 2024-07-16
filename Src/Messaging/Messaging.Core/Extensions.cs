using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("FastEndpoints")]
[assembly: InternalsVisibleTo("FastEndpoints.Messaging.Remote")]
[assembly: InternalsVisibleTo("FastEndpoints.Messaging.Remote.Core")]

namespace FastEndpoints;

static class Extensions
{
    internal static string ToHash(this string input)
        => Md5.ComputeHashString(input); // because the .NET MD5 class doesn't work in blazor wasm
}

static class Md5
{
    internal static byte[] ComputeHash(byte[] input)
    {
        var num = 1732584193u;
        var num2 = 4023233417u;
        var num3 = 2562383102u;
        var num4 = 271733878u;
        var num5 = (56 - (input.Length + 1) % 64) % 64;
        var array = new byte[input.Length + 1 + num5 + 8];
        Array.Copy(input, array, input.Length);
        array[input.Length] = 128;
        Array.Copy(BitConverter.GetBytes(input.Length * 8), 0, array, array.Length - 8, 4);

        for (var i = 0; i < array.Length / 64; i++)
        {
            var array2 = new uint[16];
            for (var j = 0; j < 16; j++)
                array2[j] = BitConverter.ToUInt32(array, i * 64 + j * 4);

            var num6 = num;
            var num7 = num2;
            var num8 = num3;
            var num9 = num4;
            var num10 = 0u;
            var num11 = 0u;
            var num12 = 0u;

            while (true)
            {
                switch (num12)
                {
                    case 0u:
                    case 1u:
                    case 2u:
                    case 3u:
                    case 4u:
                    case 5u:
                    case 6u:
                    case 7u:
                    case 8u:
                    case 9u:
                    case 10u:
                    case 11u:
                    case 12u:
                    case 13u:
                    case 14u:
                    case 15u:
                        num10 = (num7 & num8) | (~num7 & num9);
                        num11 = num12;

                        goto IL_0138;
                    case 16u:
                    case 17u:
                    case 18u:
                    case 19u:
                    case 20u:
                    case 21u:
                    case 22u:
                    case 23u:
                    case 24u:
                    case 25u:
                    case 26u:
                    case 27u:
                    case 28u:
                    case 29u:
                    case 30u:
                    case 31u:
                    case 32u:
                    case 33u:
                    case 34u:
                    case 35u:
                    case 36u:
                    case 37u:
                    case 38u:
                    case 39u:
                    case 40u:
                    case 41u:
                    case 42u:
                    case 43u:
                    case 44u:
                    case 45u:
                    case 46u:
                    case 47u:
                    case 48u:
                    case 49u:
                    case 50u:
                    case 51u:
                    case 52u:
                    case 53u:
                    case 54u:
                    case 55u:
                    case 56u:
                    case 57u:
                    case 58u:
                    case 59u:
                    case 60u:
                    case 61u:
                    case 62u:
                    case 63u:
                        if (num12 is >= 16 and <= 31)
                        {
                            num10 = (num9 & num7) | (~num9 & num8);
                            num11 = (5 * num12 + 1) % 16u;
                        }
                        else if (num12 is >= 32 and <= 47)
                        {
                            num10 = num7 ^ num8 ^ num9;
                            num11 = (3 * num12 + 5) % 16u;
                        }
                        else if (num12 >= 48)
                        {
                            num10 = num8 ^ (num7 | ~num9);
                            num11 = 7 * num12 % 16u;
                        }

                        goto IL_0138;
                }

                break;

                IL_0138:
                var num13 = num9;
                num9 = num8;
                num8 = num7;
                num7 += LeftRotate(num6 + num10 + _k[num12] + array2[num11], _s[num12]);
                num6 = num13;
                num12++;
            }

            num += num6;
            num2 += num7;
            num3 += num8;
            num4 += num9;
        }
        var hashBytes = new byte[16];
        BitConverter.GetBytes(num).CopyTo(hashBytes, 0);
        BitConverter.GetBytes(num2).CopyTo(hashBytes, 4);
        BitConverter.GetBytes(num3).CopyTo(hashBytes, 8);
        BitConverter.GetBytes(num4).CopyTo(hashBytes, 12);

        return hashBytes;
    }

    internal static string ComputeHashString(string input)
        => ComputeHashString(Encoding.UTF8.GetBytes(input));

    internal static string ComputeHashString(byte[] input)
    {
        var sb = new StringBuilder();
        var hashBytes = ComputeHash(input);
        for (var i = 0; i < hashBytes.Length; i++)
            sb.Append(hashBytes[i].ToString("x2"));

        return sb.ToString();
    }

    static readonly int[] _s =
    [
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12,
        17, 22, 7, 12, 17, 22, 5, 9, 14, 20,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9,
        14, 20, 4, 11, 16, 23, 4, 11, 16, 23,
        4, 11, 16, 23, 4, 11, 16, 23, 6, 10,
        15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
        6, 10, 15, 21
    ];

    static readonly uint[] _k =
    [
        3614090360u, 3905402710u, 606105819u, 3250441966u, 4118548399u, 1200080426u, 2821735955u, 4249261313u, 1770035416u, 2336552879u,
        4294925233u, 2304563134u, 1804603682u, 4254626195u, 2792965006u, 1236535329u, 4129170786u, 3225465664u, 643717713u, 3921069994u,
        3593408605u, 38016083u, 3634488961u, 3889429448u, 568446438u, 3275163606u, 4107603335u, 1163531501u, 2850285829u, 4243563512u,
        1735328473u, 2368359562u, 4294588738u, 2272392833u, 1839030562u, 4259657740u, 2763975236u, 1272893353u, 4139469664u, 3200236656u,
        681279174u, 3936430074u, 3572445317u, 76029189u, 3654602809u, 3873151461u, 530742520u, 3299628645u, 4096336452u, 1126891415u,
        2878612391u, 4237533241u, 1700485571u, 2399980690u, 4293915773u, 2240044497u, 1873313359u, 4264355552u, 2734768916u, 1309151649u,
        4149444226u, 3174756917u, 718787259u, 3951481745u
    ];

    static uint LeftRotate(uint x, int c)
        => (x << c) | (x >> (32 - c));
}