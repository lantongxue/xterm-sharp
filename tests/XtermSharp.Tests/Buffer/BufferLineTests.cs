using XtermSharp.Internal;

namespace XtermSharp.Tests.Buffer;

public sealed class BufferLineTests
{
    private static readonly CellData Blank = CellData.Blank(CellStyle.Default);

    [UpstreamFact("XTJS-0065", "AttributeData extended attributes hasExtendedAttrs")]
    public void AttributeData_HasExtendedAttributes()
    {
        var attributes = new AttributeData();
        Assert.False(attributes.HasExtendedAttrs());
        attributes.HasExtendedAttributes = true;
        Assert.True(attributes.HasExtendedAttrs());
    }

    [UpstreamFact("XTJS-0066", "AttributeData extended attributes getUnderlineColor - P256")]
    public void AttributeData_GetsPaletteUnderlineColor()
    {
        var attributes = new AttributeData();
        attributes.Extended.UnderlineColor = TerminalColor.Palette(45);
        Assert.Equal(-1, attributes.GetUnderlineColor());
        attributes.HasExtendedAttributes = true;
        Assert.Equal(45, attributes.GetUnderlineColor());
        attributes.Extended.UnderlineColor = TerminalColor.Default;
        attributes.Foreground = TerminalColor.Palette(123);
        Assert.Equal(123, attributes.GetUnderlineColor());
    }

    [UpstreamFact("XTJS-0067", "AttributeData extended attributes getUnderlineColor - RGB")]
    public void AttributeData_GetsRgbUnderlineColor()
    {
        var attributes = new AttributeData();
        attributes.Extended.UnderlineColor = TerminalColor.Rgb(1, 2, 3);
        Assert.Equal(-1, attributes.GetUnderlineColor());
        attributes.HasExtendedAttributes = true;
        Assert.Equal(0x010203, attributes.GetUnderlineColor());
        attributes.Extended.UnderlineColor = TerminalColor.Default;
        attributes.Foreground = TerminalColor.Palette(123);
        Assert.Equal(123, attributes.GetUnderlineColor());
    }

    [UpstreamFact("XTJS-0068", "AttributeData extended attributes getUnderlineColorMode / isUnderlineColorRGB / isUnderlineColorPalette / isUnderlineColorDefault")]
    public void AttributeData_ReportsUnderlineColorModes()
    {
        var attributes = new AttributeData();
        attributes.Extended.UnderlineColor = TerminalColor.Rgb(1, 2, 3);
        Assert.Equal(TerminalColorMode.Default, attributes.GetUnderlineColorMode());
        Assert.True(attributes.IsUnderlineColorDefault());
        attributes.Foreground = TerminalColor.Rgb(4, 5, 6);
        Assert.True(attributes.IsUnderlineColorRgb());
        attributes.HasExtendedAttributes = true;
        attributes.Extended.UnderlineColor = TerminalColor.Palette(45);
        Assert.True(attributes.IsUnderlineColorPalette());
        attributes.Extended.UnderlineColor = TerminalColor.Rgb(1, 2, 3);
        Assert.True(attributes.IsUnderlineColorRgb());
    }

    [UpstreamFact("XTJS-0069", "AttributeData extended attributes getUnderlineStyle")]
    public void AttributeData_GetsUnderlineStyle()
    {
        var attributes = new AttributeData();
        Assert.Equal(TerminalUnderlineStyle.None, attributes.GetUnderlineStyle());
        attributes.Extended.UnderlineStyle = TerminalUnderlineStyle.Curly;
        Assert.Equal(TerminalUnderlineStyle.None, attributes.GetUnderlineStyle());
        attributes.Underline = true;
        Assert.Equal(TerminalUnderlineStyle.Single, attributes.GetUnderlineStyle());
        attributes.HasExtendedAttributes = true;
        Assert.Equal(TerminalUnderlineStyle.Curly, attributes.GetUnderlineStyle());
        attributes.Underline = false;
        Assert.Equal(TerminalUnderlineStyle.None, attributes.GetUnderlineStyle());
    }

    [UpstreamFact("XTJS-0070", "AttributeData extended attributes getUnderlineVariantOffset")]
    public void AttributeData_GetsUnderlineVariantOffset()
    {
        var attributes = new AttributeData();
        Assert.Equal(0, attributes.GetUnderlineVariantOffset());
        for (int offset = 0; offset < 8; offset++)
        {
            attributes.Extended.UnderlineVariantOffset = offset;
            Assert.Equal(offset, attributes.GetUnderlineVariantOffset());
        }
    }

    [UpstreamFact("XTJS-0071", "CellData CharData <--> CellData equality")]
    public void CellData_TextRoundTripsAcrossRepresentations()
    {
        AssertCell(CellData.FromText("a", 1, CellStyle.Default), "a", 1, false);
        AssertCell(CellData.FromText("e\u0301", 1, CellStyle.Default), "e\u0301", 1, true);
        AssertCell(CellData.FromText("𝄞", 1, CellStyle.Default), "𝄞", 1, false);
        AssertCell(CellData.FromText("𓂀\u0301", 1, CellStyle.Default), "𓂀\u0301", 1, true);
        AssertCell(CellData.FromText("１", 2, CellStyle.Default), "１", 2, false);
    }

    [UpstreamFact("XTJS-0072", "BufferLine ctor")]
    public void Constructor_InitializesLengthWrapStateAndCells()
    {
        var empty = new BufferLine(0, CellStyle.Default);
        Assert.Equal(0, empty.Length);
        Assert.False(empty.IsWrapped);
        var line = new BufferLine(10, CellStyle.Default, true);
        Assert.Equal(10, line.Length);
        Assert.True(line.IsWrapped);
        Assert.Equal(string.Empty, line.GetCell(0).GetText());
        var filled = new BufferLine(10, CellData.FromText("a", 1, Style(123)), true);
        Assert.Equal("a", filled.GetCell(0).GetText());
        Assert.Equal(Style(123), filled.GetCell(0).Style);
    }

    [UpstreamFact("XTJS-0073", "BufferLine insertCells")]
    public void InsertCells_ShiftsAndFillsCells()
    {
        BufferLine line = Line("abc");
        line.InsertCells(1, 3, CellData.FromText("d", 1, Style(4)));
        Assert.Equal("add", line.TranslateToString());
        Assert.Equal([1, 4, 4], line.CopyCells(3).Select(cell => cell.Style.HyperlinkId));
    }

    [UpstreamFact("XTJS-0074", "BufferLine deleteCells")]
    public void DeleteCells_ShiftsAndFillsCells()
    {
        BufferLine line = Line("abcde");
        line.DeleteCells(1, 2, CellData.FromText("f", 1, Style(6)));
        Assert.Equal("adeff", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0075", "BufferLine replaceCells")]
    public void ReplaceCells_ReplacesRequestedRange()
    {
        BufferLine line = Line("abcde");
        line.ReplaceCells(2, 4, CellData.FromText("f", 1, Style(6)));
        Assert.Equal("abffe", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0076", "BufferLine fill")]
    public void Fill_ReplacesEveryCell()
    {
        BufferLine line = Line("abcde");
        line.Fill(CellData.FromText("z", 1, Style(123)));
        Assert.Equal("zzzzz", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0077", "BufferLine clone")]
    public void Clone_CopiesCellsLengthAndWrapState()
    {
        BufferLine line = Line("abcde", true);
        BufferLine clone = line.Clone();
        Assert.NotSame(line, clone);
        Assert.Equal(line.CopyCells(line.Length), clone.CopyCells(clone.Length));
        Assert.Equal(line.IsWrapped, clone.IsWrapped);
    }

    [UpstreamFact("XTJS-0078", "BufferLine copyFrom")]
    public void CopyFrom_CopiesCellsLengthAndWrapState()
    {
        BufferLine source = Line("abcde");
        var destination = new BufferLine(3, CellData.FromText("z", 1, CellStyle.Default), true);
        destination.CopyFrom(source);
        Assert.Equal(source.CopyCells(source.Length), destination.CopyCells(destination.Length));
        Assert.Equal(source.IsWrapped, destination.IsWrapped);
    }

    [UpstreamFact("XTJS-0079", "BufferLine should support combining chars")]
    public void CombiningCharacters_SurviveConstructionCopyAndClone()
    {
        CellData combined = CellData.FromText("e\u0301", 1, Style(1));
        var line = new BufferLine(2, combined);
        Assert.All(line.CopyCells(2), cell => Assert.Equal("e\u0301", cell.GetText()));
        var copied = new BufferLine(5, CellData.FromText("a", 1, Style(1)), true);
        copied.CopyFrom(line);
        Assert.Equal(line.CopyCells(2), copied.CopyCells(2));
        Assert.Equal(line.CopyCells(2), line.Clone().CopyCells(2));
    }

    [UpstreamFact("XTJS-0080", "BufferLine resize enlarge(false)")]
    public void Resize_EnlargesUnwrappedLine()
    {
        var line = new BufferLine(5, CellData.FromText("a", 1, Style(1)));
        line.Resize(10, CellData.FromText("a", 1, Style(1)));
        Assert.Equal("aaaaaaaaaa", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0081", "BufferLine resize enlarge(true)")]
    public void Resize_EnlargesWrappedLine()
    {
        var line = new BufferLine(5, CellData.FromText("a", 1, Style(1)), true);
        line.Resize(10, CellData.FromText("a", 1, Style(1)));
        Assert.Equal("aaaaaaaaaa", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0082", "BufferLine resize shrink(true) - should apply new size")]
    public void Resize_ShrinksToRequestedSize()
    {
        var line = new BufferLine(10, CellData.FromText("a", 1, Style(1)));
        line.Resize(5, CellData.FromText("a", 1, Style(1)));
        Assert.Equal(5, line.Length);
        Assert.Equal("aaaaa", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0083", "BufferLine resize shrink to 0 length")]
    public void Resize_ShrinksToZero()
    {
        var line = new BufferLine(10, CellData.FromText("a", 1, Style(1)));
        line.Resize(0, CellData.FromText("a", 1, Style(1)));
        Assert.Equal(0, line.Length);
        Assert.Equal(string.Empty, line.TranslateToString());
    }

    [UpstreamFact("XTJS-0084", "BufferLine resize should remove combining data on replaced cells after shrinking then enlarging")]
    public void Resize_RemovesCutOffCombinedDataBeforeEnlarging()
    {
        var line = new BufferLine(10, CellData.FromText("a", 1, Style(1)));
        line.SetCell(2, CellData.FromText("😁", 1, CellStyle.Default));
        line.SetCell(9, CellData.FromText("😁", 1, CellStyle.Default));
        line.Resize(5, CellData.FromText("a", 1, Style(1)));
        line.Resize(10, CellData.FromText("a", 1, Style(1)));
        Assert.Equal("aa😁aaaaaaa", line.TranslateToString());
        Assert.Equal(1, line.CopyCells(10).Count(cell => cell.GetText() == "😁"));
    }

    [UpstreamFact("XTJS-0085", "BufferLine getTrimLength empty line")]
    public void GetTrimmedLength_EmptyLine() => Assert.Equal(0, new BufferLine(10, Blank).GetTrimmedLength());

    [UpstreamFact("XTJS-0086", "BufferLine getTrimLength ASCII")]
    public void GetTrimmedLength_Ascii() => Assert.Equal(3, SparseLine("a", 1).GetTrimmedLength());

    [UpstreamFact("XTJS-0087", "BufferLine getTrimLength surrogate")]
    public void GetTrimmedLength_Surrogate() => Assert.Equal(3, SparseLine("𝄞", 1).GetTrimmedLength());

    [UpstreamFact("XTJS-0088", "BufferLine getTrimLength combining")]
    public void GetTrimmedLength_Combining() => Assert.Equal(3, SparseLine("e\u0301", 1).GetTrimmedLength());

    [UpstreamFact("XTJS-0089", "BufferLine getTrimLength fullwidth")]
    public void GetTrimmedLength_FullWidth()
    {
        BufferLine line = SparseLine("１", 2);
        line.SetCell(3, new CellData { Width = 0, Style = CellStyle.Default });
        Assert.Equal(4, line.GetTrimmedLength());
    }

    [UpstreamFact("XTJS-0090", "BufferLine translateToString with and w'o trimming empty line")]
    public void TranslateToString_EmptyLine()
    {
        var columns = new List<int>();
        var line = new BufferLine(10, Blank);
        Assert.Equal("          ", line.TranslateToString(false, null, null, columns));
        Assert.Equal(Enumerable.Range(0, 11), columns);
        Assert.Equal(string.Empty, line.TranslateToString(true, null, null, columns));
        Assert.Equal([0], columns);
    }

    [UpstreamFact("XTJS-0091", "BufferLine translateToString with and w'o trimming ASCII")]
    public void TranslateToString_Ascii() => AssertTranslationCases("a", 1, "a a aa    ", "a a aa", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

    [UpstreamFact("XTJS-0092", "BufferLine translateToString with and w'o trimming surrogate")]
    public void TranslateToString_Surrogate()
    {
        BufferLine line = TranslationLine("𝄞", 1);
        var columns = new List<int>();
        Assert.Equal("a 𝄞 𝄞𝄞    ", line.TranslateToString(false, null, null, columns));
        Assert.Equal([0, 1, 2, 2, 3, 4, 4, 5, 5, 6, 7, 8, 9, 10], columns);
        Assert.Equal("a 𝄞 𝄞𝄞", line.TranslateToString(true, null, null, columns));
        Assert.Equal([0, 1, 2, 2, 3, 4, 4, 5, 5, 6], columns);
    }

    [UpstreamFact("XTJS-0093", "BufferLine translateToString with and w'o trimming combining")]
    public void TranslateToString_Combining()
    {
        BufferLine line = TranslationLine("e\u0301", 1);
        var columns = new List<int>();
        Assert.Equal("a e\u0301 e\u0301e\u0301    ", line.TranslateToString(false, null, null, columns));
        Assert.Equal([0, 1, 2, 2, 3, 4, 4, 5, 5, 6, 7, 8, 9, 10], columns);
        Assert.Equal("a e\u0301 e\u0301e\u0301", line.TranslateToString(true, null, null, columns));
    }

    [UpstreamFact("XTJS-0094", "BufferLine translateToString with and w'o trimming fullwidth")]
    public void TranslateToString_FullWidth()
    {
        var line = new BufferLine(10, Blank);
        line.SetCell(0, CellData.FromText("a", 1, Style(1)));
        SetWide(line, 2, "１");
        SetWide(line, 5, "１");
        SetWide(line, 7, "１");
        var columns = new List<int>();
        Assert.Equal("a １ １１ ", line.TranslateToString(false, null, null, columns));
        Assert.Equal([0, 1, 2, 4, 5, 7, 9, 10], columns);
        Assert.Equal("a １ １１", line.TranslateToString(true, null, null, columns));
        Assert.Equal("a １ １", line.TranslateToString(false, 0, 6, columns));
        Assert.Equal([0, 1, 2, 4, 5, 7], columns);
    }

    [UpstreamFact("XTJS-0095", "BufferLine translateToString with and w'o trimming space at end")]
    public void TranslateToString_PreservesExplicitTrailingSpaceWhenTrimmed()
    {
        BufferLine line = TranslationLine("a", 1);
        line.SetCell(6, CellData.FromText(" ", 1, Style(1)));
        var columns = new List<int>();
        Assert.Equal("a a aa    ", line.TranslateToString(false, null, null, columns));
        Assert.Equal("a a aa ", line.TranslateToString(true, null, null, columns));
    }

    [UpstreamFact("XTJS-0096", "BufferLine translateToString with and w'o trimming should always return some sane value")]
    public void TranslateToString_ReturnsSaneValueForEmptyWidthCells()
    {
        var line = new BufferLine(10, Blank);
        Assert.Equal("          ", line.TranslateToString());
        Assert.Equal(string.Empty, line.TranslateToString(true));
    }

    [UpstreamFact("XTJS-0097", "BufferLine translateToString with and w'o trimming should work with endCol=0")]
    public void TranslateToString_WorksWithZeroEndColumn()
    {
        BufferLine line = SparseLine("a", 1);
        var columns = new List<int>();
        Assert.Equal(string.Empty, line.TranslateToString(true, 0, 0, columns));
        Assert.Equal([0], columns);
    }

    [UpstreamFact("XTJS-0098", "BufferLine addCharToCell should set width to 1 for empty cell")]
    public void AddCodePointToCell_SetsWidthOneForEmptyCell()
    {
        var line = new BufferLine(3, Blank);
        line.AddCodePointToCell(0, 0x301, 0);
        CellData cell = line.GetCell(0);
        Assert.Equal("\u0301", cell.GetText());
        Assert.Equal(1, cell.Width);
        Assert.False(cell.IsCombined);
    }

    [UpstreamFact("XTJS-0099", "BufferLine addCharToCell should add char to combining string in cell")]
    public void AddCodePointToCell_AppendsToCombinedString()
    {
        var line = new BufferLine(3, Blank);
        line.SetCell(0, CellData.FromText("e\u0301", 1, Style(123)));
        line.AddCodePointToCell(0, 0x301, 0);
        Assert.Equal("e\u0301\u0301", line.GetCell(0).GetText());
        Assert.True(line.GetCell(0).IsCombined);
    }

    [UpstreamFact("XTJS-0100", "BufferLine addCharToCell should create combining string on taken cell")]
    public void AddCodePointToCell_CreatesCombinedStringOnOccupiedCell()
    {
        var line = new BufferLine(3, Blank);
        line.SetCell(0, CellData.FromText("e", 1, Style(123)));
        line.AddCodePointToCell(0, 0x301, 0);
        Assert.Equal("e\u0301", line.GetCell(0).GetText());
        Assert.True(line.GetCell(0).IsCombined);
    }

    [UpstreamFact("XTJS-0101", "BufferLine correct fullwidth handling insert - wide char at pos")]
    public void FullWidth_InsertAtWideCharacterPosition()
    {
        BufferLine line = WideCurrencyLine();
        CellData a = CellData.FromText("a", 1, Style(1));
        line.InsertCells(9, 1, a);
        Assert.Equal("￥￥￥￥ a", line.TranslateToString());
        line.InsertCells(8, 1, a);
        Assert.Equal("￥￥￥￥a ", line.TranslateToString());
        line.InsertCells(1, 1, a);
        Assert.Equal(" a ￥￥￥a", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0102", "BufferLine correct fullwidth handling insert - wide char at end")]
    public void FullWidth_InsertAtLineEnd()
    {
        BufferLine line = WideCurrencyLine();
        CellData a = CellData.FromText("a", 1, Style(1));
        line.InsertCells(0, 3, a);
        Assert.Equal("aaa￥￥￥ ", line.TranslateToString());
        line.InsertCells(4, 1, a);
        Assert.Equal("aaa a ￥￥", line.TranslateToString());
        line.InsertCells(4, 1, a);
        Assert.Equal("aaa aa ￥ ", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0103", "BufferLine correct fullwidth handling delete")]
    public void FullWidth_DeleteRepairsSplitPairs()
    {
        BufferLine line = WideCurrencyLine();
        CellData a = CellData.FromText("a", 1, Style(1));
        line.DeleteCells(0, 1, a);
        Assert.Equal(" ￥￥￥￥a", line.TranslateToString());
        line.DeleteCells(5, 2, a);
        Assert.Equal(" ￥￥￥aaa", line.TranslateToString());
        line.DeleteCells(0, 2, a);
        Assert.Equal(" ￥￥aaaaa", line.TranslateToString());
    }

    [UpstreamFact("XTJS-0104", "BufferLine correct fullwidth handling replace - start at 0")]
    public void FullWidth_ReplaceFromStartRepairsSplitPairs()
    {
        AssertWideReplacement(0, 1, "a ￥￥￥￥");
        AssertWideReplacement(0, 2, "aa￥￥￥￥");
        AssertWideReplacement(0, 3, "aaa ￥￥￥");
        AssertWideReplacement(0, 8, "aaaaaaaa￥");
        AssertWideReplacement(0, 9, "aaaaaaaaa ");
        AssertWideReplacement(0, 10, "aaaaaaaaaa");
    }

    [UpstreamFact("XTJS-0105", "BufferLine correct fullwidth handling replace - start at 1")]
    public void FullWidth_ReplaceFromSecondCellRepairsSplitPairs()
    {
        AssertWideReplacement(1, 2, " a￥￥￥￥");
        AssertWideReplacement(1, 3, " aa ￥￥￥");
        AssertWideReplacement(1, 4, " aaa￥￥￥");
        AssertWideReplacement(1, 8, " aaaaaaa￥");
        AssertWideReplacement(1, 9, " aaaaaaaa ");
        AssertWideReplacement(1, 10, " aaaaaaaaa");
    }

    [UpstreamFact("XTJS-0106", "BufferLine extended attributes setCells")]
    public void ExtendedAttributes_SetCellsPreservesReferences()
    {
        BufferLine line = ExtendedAttributeLine();
        Assert.Null(line.GetCell(0).Extended);
        Assert.Equal(TerminalUnderlineStyle.Curly, line.GetCell(1).Extended!.UnderlineStyle);
        Assert.Same(line.GetCell(1).Extended, line.GetCell(2).Extended);
        Assert.Equal(TerminalUnderlineStyle.Dotted, line.GetCell(3).Extended!.UnderlineStyle);
        Assert.NotSame(line.GetCell(1).Extended, line.GetCell(3).Extended);
        Assert.Null(line.GetCell(4).Extended);
    }

    [UpstreamFact("XTJS-0107", "BufferLine extended attributes loadCell")]
    public void ExtendedAttributes_GetCellLoadsExpectedReferences()
    {
        BufferLine line = ExtendedAttributeLine();
        CellData first = line.GetCell(1);
        CellData second = line.GetCell(2);
        CellData third = line.GetCell(3);
        Assert.Same(first.Extended, second.Extended);
        Assert.NotSame(second.Extended, third.Extended);
    }

    [UpstreamFact("XTJS-0108", "BufferLine extended attributes fill")]
    public void ExtendedAttributes_FillCopiesAttributesToEveryCell()
    {
        var line = new BufferLine(3, Blank);
        CellData cell = ExtendedCell(TerminalUnderlineStyle.Curly);
        line.Fill(cell);
        Assert.All(line.CopyCells(3), value => Assert.Equal(TerminalUnderlineStyle.Curly, value.Extended!.UnderlineStyle));
    }

    [UpstreamFact("XTJS-0109", "BufferLine extended attributes insertCells")]
    public void ExtendedAttributes_InsertCellsMovesAndFillsReferences()
    {
        var line = new BufferLine(5, Blank);
        CellData curly = ExtendedCell(TerminalUnderlineStyle.Curly);
        line.InsertCells(1, 3, curly);
        Assert.Equal([null, TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Curly, null], Styles(line));
        CellData dotted = ExtendedCell(TerminalUnderlineStyle.Dotted);
        line.InsertCells(2, 2, dotted);
        Assert.Equal([null, TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Dotted, TerminalUnderlineStyle.Dotted, TerminalUnderlineStyle.Curly], Styles(line));
    }

    [UpstreamFact("XTJS-0110", "BufferLine extended attributes deleteCells")]
    public void ExtendedAttributes_DeleteCellsMovesAndFillsReferences()
    {
        var line = new BufferLine(5, ExtendedCell(TerminalUnderlineStyle.Curly));
        line.DeleteCells(1, 3, ExtendedCell(TerminalUnderlineStyle.Double));
        Assert.Equal([TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Double, TerminalUnderlineStyle.Double, TerminalUnderlineStyle.Double], Styles(line));
    }

    [UpstreamFact("XTJS-0111", "BufferLine extended attributes replaceCells")]
    public void ExtendedAttributes_ReplaceCellsReplacesReferences()
    {
        var line = new BufferLine(5, ExtendedCell(TerminalUnderlineStyle.Curly));
        line.ReplaceCells(1, 3, ExtendedCell(TerminalUnderlineStyle.Double));
        Assert.Equal([TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Double, TerminalUnderlineStyle.Double, TerminalUnderlineStyle.Curly, TerminalUnderlineStyle.Curly], Styles(line));
    }

    [UpstreamFact("XTJS-0112", "BufferLine extended attributes clone")]
    public void ExtendedAttributes_ClonePreservesCanonicalReferences()
    {
        BufferLine line = ExtendedAttributeLine();
        BufferLine clone = line.Clone();
        for (int index = 0; index < line.Length; index++)
        {
            Assert.Same(line.GetCell(index).Extended, clone.GetCell(index).Extended);
        }
    }

    [UpstreamFact("XTJS-0113", "BufferLine extended attributes copyFrom")]
    public void ExtendedAttributes_CopyFromPreservesCanonicalReferences()
    {
        BufferLine source = ExtendedAttributeLine();
        var destination = new BufferLine(5, CellData.FromText("b", 1, CellStyle.Default));
        destination.CopyFrom(source);
        for (int index = 0; index < source.Length; index++)
        {
            Assert.Same(source.GetCell(index).Extended, destination.GetCell(index).Extended);
        }
    }

    [UpstreamFact("XTJS-0114", "BufferLine extended attributes should cache canonical string translations")]
    public void StringCache_CachesCanonicalTranslations()
    {
        var cache = new BufferLineStringCache();
        var line = new BufferLine(5, Blank, stringCache: cache);
        line.SetCell(0, CellData.FromText("a", 1, CellStyle.Default));
        line.SetCell(1, CellData.FromText("b", 1, CellStyle.Default));
        line.SetCell(2, CellData.FromText("c", 1, CellStyle.Default));
        Assert.Equal("abc", line.TranslateToString(true));
        Assert.Equal("abc", line.CachedString);
        Assert.True(line.IsCachedStringTrimmed);
        Assert.Equal("abc  ", line.TranslateToString());
        Assert.Equal("abc  ", line.CachedString);
        Assert.False(line.IsCachedStringTrimmed);
        Assert.Equal("abc", line.TranslateToString(true));
        line.SetCachedString("cached-non-trimmed  ", false);
        Assert.Equal("cached-non-trimmed", line.TranslateToString(true));
        line.SetCachedString("cached-trimmed", true);
        Assert.Equal("abc  ", line.TranslateToString());
        Assert.Equal("ab", line.TranslateToString(false, 0, 2));
        cache.Dispose();
    }

    [UpstreamFact("XTJS-0115", "BufferLine extended attributes should invalidate cached canonical strings on line mutations")]
    public void StringCache_IsInvalidatedByEveryLineMutation()
    {
        AssertMutationInvalidates(line => line.SetCell(0, CellData.FromText("b", 1, CellStyle.Default)));
        AssertMutationInvalidates(line => line.SetCellFromCodePoint(0, 'b', 1, CellStyle.Default));
        AssertMutationInvalidates(line => line.AddCodePointToCell(0, 0x301, 0));
        AssertMutationInvalidates(line => line.InsertCells(1, 1, CellData.FromText("b", 1, CellStyle.Default)));
        AssertMutationInvalidates(line => line.DeleteCells(1, 1, CellData.FromText("b", 1, CellStyle.Default)));
        AssertMutationInvalidates(line => line.ReplaceCells(1, 3, CellData.FromText("b", 1, CellStyle.Default)));
        AssertMutationInvalidates(line => line.Resize(6, CellData.FromText("b", 1, CellStyle.Default)));
        AssertMutationInvalidates(line => line.Fill(CellData.FromText("b", 1, CellStyle.Default)));
        AssertMutationInvalidates(line => line.CopyFrom(new BufferLine(5, CellData.FromText("x", 1, CellStyle.Default))));
        AssertMutationInvalidates(line => line.CopyCellsFrom(new BufferLine(5, CellData.FromText("x", 1, CellStyle.Default)), 0, 0, 2, false));
    }

    private static void AssertCell(CellData cell, string text, byte width, bool combined)
    {
        Assert.Equal(text, cell.GetText());
        Assert.Equal(width, cell.Width);
        Assert.Equal(combined, cell.IsCombined);
    }

    private static CellStyle Style(int id) => CellStyle.Default with { HyperlinkId = id };

    private static BufferLine Line(string text, bool wrapped = false)
    {
        var line = new BufferLine(text.Length, Blank, wrapped);
        for (int index = 0; index < text.Length; index++)
        {
            line.SetCell(index, CellData.FromText(text[index].ToString(), 1, Style(index + 1)));
        }
        return line;
    }

    private static BufferLine SparseLine(string text, byte width)
    {
        var line = new BufferLine(10, Blank);
        line.SetCell(0, CellData.FromText("a", 1, Style(1)));
        line.SetCell(2, CellData.FromText(text, width, Style(1)));
        return line;
    }

    private static BufferLine TranslationLine(string text, byte width)
    {
        var line = new BufferLine(10, Blank);
        line.SetCell(0, CellData.FromText("a", 1, Style(1)));
        line.SetCell(2, CellData.FromText(text, width, Style(1)));
        line.SetCell(4, CellData.FromText(text, width, Style(1)));
        line.SetCell(5, CellData.FromText(text, width, Style(1)));
        return line;
    }

    private static void AssertTranslationCases(string text, byte width, string full, string trimmed, int[] columnsExpected)
    {
        BufferLine line = TranslationLine(text, width);
        var columns = new List<int>();
        Assert.Equal(full, line.TranslateToString(false, null, null, columns));
        Assert.Equal(columnsExpected, columns);
        Assert.Equal(trimmed, line.TranslateToString(true, null, null, columns));
        Assert.Equal("a a a", line.TranslateToString(false, 0, 5, columns));
        Assert.Equal("a a ", line.TranslateToString(true, 0, 4, columns));
        Assert.Equal("a a", line.TranslateToString(false, 0, 3, columns));
    }

    private static void SetWide(BufferLine line, int column, string text)
    {
        line.SetCell(column, CellData.FromText(text, 2, Style(1)));
        line.SetCell(column + 1, new CellData { Width = 0, Style = CellStyle.Default });
    }

    private static BufferLine WideCurrencyLine()
    {
        var line = new BufferLine(10, Blank);
        for (int index = 0; index < line.Length; index += 2)
        {
            line.SetCell(index, CellData.FromText("￥", 2, Style(1)));
        }
        return line;
    }

    private static void AssertWideReplacement(int start, int end, string expected)
    {
        BufferLine line = WideCurrencyLine();
        line.ReplaceCells(start, end, CellData.FromText("a", 1, Style(1)));
        Assert.Equal(expected, line.TranslateToString());
    }

    private static CellData ExtendedCell(TerminalUnderlineStyle style)
    {
        CellData cell = CellData.FromText("a", 1, CellStyle.Default with { UnderlineStyle = style });
        cell.Extended = new ExtendedCellAttributes { UnderlineStyle = style };
        return cell;
    }

    private static BufferLine ExtendedAttributeLine()
    {
        var line = new BufferLine(5, Blank);
        line.SetCell(0, CellData.FromText("a", 1, CellStyle.Default));
        CellData curly = ExtendedCell(TerminalUnderlineStyle.Curly);
        line.SetCell(1, curly);
        CellData same = curly;
        same.CodePoint = 'A';
        line.SetCell(2, same);
        CellData dotted = curly;
        dotted.Extended = curly.Extended!.Clone();
        dotted.Extended.UnderlineStyle = TerminalUnderlineStyle.Dotted;
        line.SetCell(3, dotted);
        CellData plain = same;
        plain.Extended = null;
        plain.Style = CellStyle.Default;
        line.SetCell(4, plain);
        return line;
    }

    private static TerminalUnderlineStyle?[] Styles(BufferLine line) =>
        line.CopyCells(line.Length).Select(cell => cell.Extended?.UnderlineStyle).ToArray();

    private static void AssertMutationInvalidates(Action<BufferLine> mutation)
    {
        var line = new BufferLine(5, CellData.FromText("a", 1, CellStyle.Default));
        line.TranslateToString(true);
        Assert.NotNull(line.CachedString);
        mutation(line);
        Assert.Null(line.CachedString);
        Assert.False(line.IsCachedStringTrimmed);
    }
}
