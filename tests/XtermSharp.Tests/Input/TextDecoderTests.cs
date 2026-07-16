using System.Text;
using System.Runtime.InteropServices;
using XtermSharp.Internal;

namespace XtermSharp.Tests.Input;

public sealed class TextDecoderTests
{
    private const int BatchSize = 8192;

    private static readonly string[] TestStrings =
    [
        "Лорем ипсум долор сит амет, ех сеа аццусам диссентиет. Ан еос стет еирмод витуперата. Иус дицерет урбанитас ет. Ан при алтера долорес сплендиде, цу яуо интегре денияуе, игнота волуптариа инструцтиор цу вим.",
        "ლორემ იფსუმ დოლორ სით ამეთ, ფაცერ მუციუს ცონსეთეთურ ყუო იდ, ფერ ვივენდუმ ყუაერენდუმ ეა, ესთ ამეთ მოვეთ სუავითათე ცუ. ვითაე სენსიბუს ან ვიხ. ეხერცი დეთერრუისსეთ უთ ყუი. ვოცენთ დებითის ადიფისცი ეთ ფერ. ნეც ან ფეუგაით ფორენსიბუს ინთერესსეთ. იდ დიცო რიდენს იუს. დისსენთიეთ ცონსეყუუნთურ სედ ნე, ნოვუმ მუნერე ეუმ ათ, ნე ეუმ ნიჰილ ირაცუნდია ურბანითას.",
        "अधिकांश अमितकुमार प्रोत्साहित मुख्य जाने प्रसारन विश्लेषण विश्व दारी अनुवादक अधिकांश नवंबर विषय गटकउसि गोपनीयता विकास जनित परस्पर गटकउसि अन्तरराष्ट्रीयकरन होसके मानव पुर्णता कम्प्युटर यन्त्रालय प्रति साधन",
        "覧六子当聞社計文護行情投身斗来。増落世的況上席備界先関権能万。本物挙歯乳全事携供板栃果以。頭月患端撤競見界記引去法条公泊候。決海備駆取品目芸方用朝示上用報。講申務紙約週堂出応理田流団幸稿。起保帯吉対阜庭支肯豪彰属本躍。量抑熊事府募動極都掲仮読岸。自続工就断庫指北速配鳴約事新住米信中験。婚浜袋著金市生交保他取情距。",
        "八メル務問へふらく博辞説いわょ読全タヨムケ東校どっ知壁テケ禁去フミ人過を装5階がねぜ法逆はじ端40落ミ予竹マヘナセ任1悪た。省ぜりせ製暇ょへそけ風井イ劣手はぼまず郵富法く作断タオイ取座ゅょが出作ホシ月給26島ツチ皇面ユトクイ暮犯リワナヤ断連こうでつ蔭柔薄とレにの。演めけふぱ損田転10得観びトげぎ王物鉄夜がまけ理惜くち牡提づ車惑参ヘカユモ長臓超漫ぼドかわ。",
        "모든 국민은 행위시의 법률에 의하여 범죄를 구성하지 아니하는 행위로 소추되지 아니하며. 전직대통령의 신분과 예우에 관하여는 법률로 정한다, 국회는 헌법 또는 법률에 특별한 규정이 없는 한 재적의원 과반수의 출석과 출석의원 과반수의 찬성으로 의결한다. 군인·군무원·경찰공무원 기타 법률이 정하는 자가 전투·훈련등 직무집행과 관련하여 받은 손해에 대하여는 법률이 정하는 보상외에 국가 또는 공공단체에 공무원의 직무상 불법행위로 인한 배상은 청구할 수 없다.",
        "كان فشكّل الشرقي مع, واحدة للمجهود تزامناً بعض بل. وتم جنوب للصين غينيا لم, ان وبدون وكسبت الأمور ذلك, أسر الخاسر الانجليزية هو. نفس لغزو مواقعها هو. الجو علاقة الصعداء انه أي, كما مع بمباركة للإتحاد الوزراء. ترتيب الأولى أن حدى, الشتوية باستحداث مدن بل, كان قد أوسع عملية. الأوضاع بالمطالبة كل قام, دون إذ شمال الربيع،. هُزم الخاصّة ٣٠ أما, مايو الصينية مع قبل.",
        "או סדר החול מיזמי קרימינולוגיה. קהילה בגרסה לויקיפדים אל היא, של צעד ציור ואלקטרוניקה. מדע מה ברית המזנון ארכיאולוגיה, אל טבלאות מבוקשים כלל. מאמרשיחהצפה העריכהגירסאות שכל אל, כתב עיצוב מושגי של. קבלו קלאסיים ב מתן. נבחרים אווירונאוטיקה אם מלא, לוח למנוע ארכיאולוגיה מה. ארץ לערוך בקרבת מונחונים או, עזרה רקטות לויקיפדים אחר גם.",
        "Лорем ლორემ अधिकांश 覧六子 八メル 모든 בקרבת 💮 😂 äggg 123€ 𝄞."
    ];

    public static TheoryData<string, int, int> StringCodePointRanges { get; } =
        CreateStringCodePointRanges();

    public static TheoryData<string, int, int> Utf8CodePointRanges { get; } =
        CreateUtf8CodePointRanges();

    [UpstreamFact("XTJS-0420", "text encodings stringFromCodePoint/utf32ToString")]
    public void Utf32_conversion_helpers()
    {
        const string value = "abcdefg";
        var data = new uint[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            data[i] = value[i];
            Assert.Equal(value[i].ToString(), TextDecoder.StringFromCodePoint(data[i]));
        }
        Assert.Equal(value, TextDecoder.Utf32ToString(data));
    }

    [UpstreamFact("XTJS-0421", "text encodings StringToUtf32 decoder test strings")]
    public void String_decoder_handles_representative_strings()
    {
        var decoder = new StringToUtf32();
        var target = new uint[500];
        foreach (string value in TestStrings)
        {
            int length = decoder.Decode(value, target);
            Assert.Equal(value, TextDecoder.Utf32ToString(target, 0, length));
            decoder.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(StringCodePointRanges))]
    public void String_decoder_handles_full_codepoint_range(string upstreamId, int minimum, int maximum)
    {
        Assert.StartsWith("XTJS-", upstreamId, StringComparison.Ordinal);
        var input = new StringBuilder();
        int count = 0;
        for (int codePoint = minimum; codePoint < maximum; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF || codePoint == 0xFEFF)
            {
                continue;
            }
            input.Append(TextDecoder.StringFromCodePoint((uint)codePoint));
            count++;
        }

        var target = new uint[count];
        int length = new StringToUtf32().Decode(input.ToString(), target);
        Assert.Equal(count, length);
        AssertDecodedValues(target, length, minimum, maximum);
        Assert.Equal(input.ToString(), TextDecoder.Utf32ToString(target, 0, length));
    }

    [UpstreamFact("XTJS-0558", "text encodings StringToUtf32 decoder full codepoint test 0xFEFF(BOM)")]
    public void String_decoder_discards_bom()
    {
        var decoder = new StringToUtf32();
        var target = new uint[5];
        Assert.Equal(0, decoder.Decode("\uFEFF", target));
        decoder.Clear();
    }

    [UpstreamFact("XTJS-0559", "text encodings StringToUtf32 decoder stream handling surrogates mixed advance by 1")]
    public void String_decoder_streams_one_utf16_code_unit_at_a_time()
    {
        var decoder = new StringToUtf32();
        var target = new uint[5];
        const string input = "Ä€𝄞Ö𝄞€Ü𝄞€";
        var decoded = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            int written = decoder.Decode(input[i].ToString(), target);
            decoded.Append(TextDecoder.Utf32ToString(target, 0, written));
        }
        Assert.Equal(input, decoded.ToString());
    }

    [UpstreamFact("XTJS-0560", "text encodings Utf8ToUtf32 decoder test strings")]
    public void Utf8_decoder_handles_representative_strings()
    {
        var decoder = new Utf8ToUtf32();
        var target = new uint[500];
        foreach (string value in TestStrings)
        {
            int length = decoder.Decode(Encoding.UTF8.GetBytes(value), target);
            Assert.Equal(value, TextDecoder.Utf32ToString(target, 0, length));
            decoder.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(Utf8CodePointRanges))]
    public void Utf8_decoder_handles_full_codepoint_range(string upstreamId, int minimum, int maximum)
    {
        Assert.StartsWith("XTJS-", upstreamId, StringComparison.Ordinal);
        var inputText = new StringBuilder();
        var inputBytes = new List<byte>((maximum - minimum) * 4);
        int count = 0;
        for (int codePoint = minimum; codePoint < maximum; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF || codePoint == 0xFEFF)
            {
                continue;
            }
            inputText.Append(TextDecoder.StringFromCodePoint((uint)codePoint));
            AppendUtf8(inputBytes, codePoint);
            count++;
        }

        var target = new uint[count];
        int length = new Utf8ToUtf32().Decode(CollectionsMarshal.AsSpan(inputBytes), target);
        Assert.Equal(count, length);
        AssertDecodedValues(target, length, minimum, maximum);
        Assert.Equal(inputText.ToString(), TextDecoder.Utf32ToString(target, 0, length));
    }

    [UpstreamFact("XTJS-0698", "text encodings Utf8ToUtf32 decoder full codepoint test 0xFEFF(BOM)")]
    public void Utf8_decoder_discards_bom()
    {
        var decoder = new Utf8ToUtf32();
        var target = new uint[5];
        Assert.Equal(0, decoder.Decode([0xEF, 0xBB, 0xBF], target));
        decoder.Clear();
    }

    [UpstreamFact("XTJS-0699", "text encodings Utf8ToUtf32 decoder stream handling 2 byte sequences - advance by 1")]
    public void Utf8_decoder_streams_two_byte_sequences_one_byte_at_a_time() =>
        Assert.Equal("ÄÖÜßöäü", DecodeInChunks([0xC3, 0x84, 0xC3, 0x96, 0xC3, 0x9C, 0xC3, 0x9F, 0xC3, 0xB6, 0xC3, 0xA4, 0xC3, 0xBC], 1));

    [UpstreamFact("XTJS-0700", "text encodings Utf8ToUtf32 decoder stream handling 2/3 byte sequences - advance by 1")]
    public void Utf8_decoder_streams_two_and_three_byte_sequences_one_byte_at_a_time() =>
        Assert.Equal("Ä€Ö€Ü€ß€ö€ä€ü", DecodeInChunks([0xC3, 0x84, 0xE2, 0x82, 0xAC, 0xC3, 0x96, 0xE2, 0x82, 0xAC, 0xC3, 0x9C, 0xE2, 0x82, 0xAC, 0xC3, 0x9F, 0xE2, 0x82, 0xAC, 0xC3, 0xB6, 0xE2, 0x82, 0xAC, 0xC3, 0xA4, 0xE2, 0x82, 0xAC, 0xC3, 0xBC], 1));

    [UpstreamFact("XTJS-0701", "text encodings Utf8ToUtf32 decoder stream handling 2/3/4 byte sequences - advance by 1")]
    public void Utf8_decoder_streams_mixed_sequences_one_byte_at_a_time() =>
        Assert.Equal("Ä€𝄞Ö𝄞€Ü𝄞€", DecodeInChunks(MixedUtf8Data(), 1));

    [UpstreamFact("XTJS-0702", "text encodings Utf8ToUtf32 decoder stream handling 2/3/4 byte sequences - advance by 2")]
    public void Utf8_decoder_streams_mixed_sequences_two_bytes_at_a_time() =>
        Assert.Equal("Ä€𝄞Ö𝄞€Ü𝄞€", DecodeInChunks(MixedUtf8Data(), 2));

    [UpstreamFact("XTJS-0703", "text encodings Utf8ToUtf32 decoder stream handling 2/3/4 byte sequences - advance by 3")]
    public void Utf8_decoder_streams_mixed_sequences_three_bytes_at_a_time() =>
        Assert.Equal("Ä€𝄞Ö𝄞€Ü𝄞€", DecodeInChunks(MixedUtf8Data(), 3));

    [UpstreamFact("XTJS-0704", "text encodings Utf8ToUtf32 decoder stream handling BOMs (3 byte sequences) - advance by 2")]
    public void Utf8_decoder_discards_streamed_boms() =>
        Assert.Equal(string.Empty, DecodeInChunks([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF], 2));

    [UpstreamFact("XTJS-0705", "text encodings Utf8ToUtf32 decoder stream handling test break after 3 bytes - issue #2495")]
    public void Utf8_decoder_resumes_four_byte_sequence_after_three_bytes()
    {
        var decoder = new Utf8ToUtf32();
        var target = new uint[5];
        byte[] input = [0xF0, 0xA0, 0x9C, 0x8E];
        Assert.Equal(0, decoder.Decode(input.AsSpan(0, 3), target));
        int written = decoder.Decode(input.AsSpan(3), target);
        Assert.Equal(1, written);
        Assert.Equal("𠜎", TextDecoder.Utf32ToString(target, 0, written));
    }

    [UpstreamFact("XTJS-0706", "text encodings Utf8ToUtf32 decoder stream handling 0x80 not swallowed in continuation A—B")]
    public void Utf8_decoder_does_not_swallow_ascii_after_three_byte_sequences() =>
        Assert.Equal("A—BA—BA—BA—BA—B", DecodeInChunks(Encoding.UTF8.GetBytes("A—BA—BA—BA—BA—B"), 2));

    [UpstreamFact("XTJS-0707", "text encodings Utf8ToUtf32 decoder stream handling 0x80 not swallowed in continuation A𐀀B")]
    public void Utf8_decoder_does_not_swallow_ascii_after_four_byte_sequences() =>
        Assert.Equal("A𐀀BA𐀀BA𐀀BA𐀀BA𐀀B", DecodeInChunks(Encoding.UTF8.GetBytes("A𐀀BA𐀀BA𐀀BA𐀀BA𐀀B"), 2));

    private static TheoryData<string, int, int> CreateStringCodePointRanges()
    {
        var data = new TheoryData<string, int, int>();
        for (int minimum = 0, id = 422; minimum < 65535; minimum += BatchSize, id++)
        {
            int maximum = Math.Min(minimum + BatchSize, 65536);
            AddRange(data, id, minimum, maximum, "text encodings StringToUtf32 decoder full codepoint test");
        }
        for (int minimum = 65536, id = 430; minimum < 0x10FFFF; minimum += BatchSize, id++)
        {
            int maximum = Math.Min(minimum + BatchSize, 0x10FFFF);
            AddRange(data, id, minimum, maximum, "text encodings StringToUtf32 decoder full codepoint test", " (surrogates)");
        }
        return data;
    }

    private static TheoryData<string, int, int> CreateUtf8CodePointRanges()
    {
        var data = new TheoryData<string, int, int>();
        for (int minimum = 0, id = 561; minimum < 65535; minimum += BatchSize, id++)
        {
            int maximum = Math.Min(minimum + BatchSize, 65536);
            AddRange(data, id, minimum, maximum, "text encodings Utf8ToUtf32 decoder full codepoint test", " (1/2/3 byte sequences)");
        }
        for (int rawMinimum = 60000, id = 569; rawMinimum < 0x10FFFF; rawMinimum += BatchSize, id++)
        {
            int minimum = Math.Max(rawMinimum, 65536);
            int maximum = Math.Min(rawMinimum + BatchSize, 0x10FFFF);
            AddRange(data, id, minimum, maximum, "text encodings Utf8ToUtf32 decoder full codepoint test", " (4 byte sequences)");
        }
        return data;
    }

    private static void AddRange(
        TheoryData<string, int, int> data,
        int id,
        int minimum,
        int maximum,
        string title,
        string suffix = "")
    {
        string upstreamId = $"XTJS-{id:0000}";
        data.Add(new TheoryDataRow<string, int, int>(upstreamId, minimum, maximum)
        {
            TestDisplayName = $"{upstreamId} {title} {FormatRange(minimum, maximum)}{suffix}"
        });
    }

    private static string FormatRange(int minimum, int maximum) =>
        $"{minimum}..{maximum} (0x{minimum:X}..0x{maximum:X})";

    private static void AssertDecodedValues(uint[] target, int length, int minimum, int maximum)
    {
        int index = 0;
        for (int codePoint = minimum; codePoint < maximum; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF || codePoint == 0xFEFF)
            {
                continue;
            }
            Assert.True(index < length, $"Decoder stopped before U+{codePoint:X}.");
            Assert.Equal((uint)codePoint, target[index++]);
        }
        Assert.Equal(length, index);
    }

    private static void AppendUtf8(List<byte> target, int codePoint)
    {
        if (codePoint < 0x80)
        {
            target.Add((byte)codePoint);
        }
        else if (codePoint < 0x800)
        {
            target.Add((byte)(0xC0 | (codePoint >> 6)));
            target.Add((byte)(0x80 | (codePoint & 0x3F)));
        }
        else if (codePoint < 0x10000)
        {
            target.Add((byte)(0xE0 | (codePoint >> 12)));
            target.Add((byte)(0x80 | ((codePoint >> 6) & 0x3F)));
            target.Add((byte)(0x80 | (codePoint & 0x3F)));
        }
        else
        {
            target.Add((byte)(0xF0 | (codePoint >> 18)));
            target.Add((byte)(0x80 | ((codePoint >> 12) & 0x3F)));
            target.Add((byte)(0x80 | ((codePoint >> 6) & 0x3F)));
            target.Add((byte)(0x80 | (codePoint & 0x3F)));
        }
    }

    private static string DecodeInChunks(byte[] input, int advance)
    {
        var decoder = new Utf8ToUtf32();
        var target = new uint[5];
        var decoded = new StringBuilder();
        for (int i = 0; i < input.Length; i += advance)
        {
            int written = decoder.Decode(input.AsSpan(i, Math.Min(advance, input.Length - i)), target);
            decoded.Append(TextDecoder.Utf32ToString(target, 0, written));
        }
        return decoded.ToString();
    }

    private static byte[] MixedUtf8Data() =>
    [
        0xC3, 0x84, 0xE2, 0x82, 0xAC, 0xF0, 0x9D, 0x84, 0x9E,
        0xC3, 0x96, 0xF0, 0x9D, 0x84, 0x9E, 0xE2, 0x82, 0xAC,
        0xC3, 0x9C, 0xF0, 0x9D, 0x84, 0x9E, 0xE2, 0x82, 0xAC
    ];
}
