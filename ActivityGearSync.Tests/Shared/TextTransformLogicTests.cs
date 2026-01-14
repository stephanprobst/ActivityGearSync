using ActivityGearSync.Shared;

namespace ActivityGearSync.Tests.Shared;

public class TextTransformLogicTests
{
    [Test]
    public async Task ApplyOperation_Set_ReplacesWithNewValue()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Original text",
            TextTransformLogic.Operations.Set,
            newValue: "New text");

        await Assert.That(result).IsEqualTo("New text");
    }

    [Test]
    public async Task ApplyOperation_Set_WithNullValue_ReturnsEmpty()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Original text",
            TextTransformLogic.Operations.Set,
            newValue: null);

        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ApplyOperation_AddPrefix_PrependsText()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Run",
            TextTransformLogic.Operations.AddPrefix,
            prefix: "Morning ");

        await Assert.That(result).IsEqualTo("Morning Run");
    }

    [Test]
    public async Task ApplyOperation_AddPrefix_WithNullPrefix_ReturnsOriginal()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Run",
            TextTransformLogic.Operations.AddPrefix,
            prefix: null);

        await Assert.That(result).IsEqualTo("Run");
    }

    [Test]
    public async Task ApplyOperation_AddSuffix_AppendsText()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Morning",
            TextTransformLogic.Operations.AddSuffix,
            suffix: " Run");

        await Assert.That(result).IsEqualTo("Morning Run");
    }

    [Test]
    public async Task ApplyOperation_AddSuffix_WithNullSuffix_ReturnsOriginal()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Morning",
            TextTransformLogic.Operations.AddSuffix,
            suffix: null);

        await Assert.That(result).IsEqualTo("Morning");
    }

    [Test]
    public async Task ApplyOperation_FindReplace_ReplacesText()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Morning Run with Friends",
            TextTransformLogic.Operations.FindReplace,
            findText: "Run",
            replaceText: "Jog");

        await Assert.That(result).IsEqualTo("Morning Jog with Friends");
    }

    [Test]
    public async Task ApplyOperation_FindReplace_IsCaseInsensitive()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Morning RUN with Friends",
            TextTransformLogic.Operations.FindReplace,
            findText: "run",
            replaceText: "Jog");

        await Assert.That(result).IsEqualTo("Morning Jog with Friends");
    }

    [Test]
    public async Task ApplyOperation_FindReplace_WithEmptyReplacement_RemovesText()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Morning Run - Easy",
            TextTransformLogic.Operations.FindReplace,
            findText: " - Easy",
            replaceText: "");

        await Assert.That(result).IsEqualTo("Morning Run");
    }

    [Test]
    public async Task ApplyOperation_UnknownOperation_ReturnsOriginal()
    {
        string result = TextTransformLogic.ApplyOperation(
            "Original text",
            "Unknown operation");

        await Assert.That(result).IsEqualTo("Original text");
    }

    [Test]
    public async Task Operations_All_ContainsAllOperations()
    {
        await Assert.That(TextTransformLogic.Operations.All.Length).IsEqualTo(4);
        await Assert.That(TextTransformLogic.Operations.All).Contains(TextTransformLogic.Operations.Set);
        await Assert.That(TextTransformLogic.Operations.All).Contains(TextTransformLogic.Operations.AddPrefix);
        await Assert.That(TextTransformLogic.Operations.All).Contains(TextTransformLogic.Operations.AddSuffix);
        await Assert.That(TextTransformLogic.Operations.All).Contains(TextTransformLogic.Operations.FindReplace);
    }
}
