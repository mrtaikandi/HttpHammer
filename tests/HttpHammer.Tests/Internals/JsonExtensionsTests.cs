using System.Text.Json;
using HttpHammer.Internals;

namespace HttpHammer.Tests.Internals;

public class JsonExtensionsTests
{
    private readonly JsonDocument _testDocument;

    public JsonExtensionsTests()
    {
        const string Json =
            """
            {
                "stringProperty": "value",
                "numberProperty": 42,
                "boolProperty": true,
                "nullProperty": null,
                "objectProperty": {
                    "nestedProperty": "nested value",
                    "deepObject": {
                        "deepProperty": "deep value"
                    }
                },
                "arrayProperty": [
                    "first",
                    "second",
                    "third"
                ],
                "objectArray": [
                    {
                        "id": "item1",
                        "name": "First Item",
                        "price": 10
                    },
                    {
                        "id": "item2",
                        "name": "Second Item",
                        "price": 20
                    },
                    {
                        "id": "item3",
                        "name": "Third Item",
                        "price": 30
                    }
                ],
                "nestedArrays": [
                    ["a", "b", "c"],
                    ["d", "e", "f"]
                ],
                "duplicateProperty": "top level",
                "level1": {
                    "duplicateProperty": "level 1",
                    "level2": {
                        "duplicateProperty": "level 2"
                    }
                }
            }
            """;

        _testDocument = JsonDocument.Parse(Json);
    }

    [Fact]
    public void TryExtractJsonValue_WithArrayIndex_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("arrayProperty[1]", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("second");
    }

    [Fact]
    public void TryExtractJsonValue_WithArraySlice_ReturnsFirstElementInSlice()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("arrayProperty[0:2]", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("first");
    }

    [Fact]
    public void TryExtractJsonValue_WithCombinedFeatures_ReturnsCorrectValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("$.objectArray[?(@.price>15)].name", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("Second Item");
    }

    [Fact]
    public void TryExtractJsonValue_WithDeeplyNestedProperty_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectProperty.deepObject.deepProperty", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("deep value");
    }

    [Fact]
    public void TryExtractJsonValue_WithEmptyPath_ReturnsEntireJson()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldNotBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithFilterExpression_ReturnsMatchingElement()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectArray[?(@.id=='item2')]", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldContain("Second Item");
    }

    [Fact]
    public void TryExtractJsonValue_WithFilterExpressionNoMatch_ReturnsFalse()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectArray[?(@.id=='nonexistent')]", out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithInvalidArrayIndex_ReturnsFalse()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("arrayProperty[10]", out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithInvalidArraySlice_ReturnsFalse()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("arrayProperty[2:1]", out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithJustRootIndicator_ReturnsEntireDocument()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("$", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldNotBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithNestedArrays_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("nestedArrays[1][2]", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("f");
    }

    [Fact]
    public void TryExtractJsonValue_WithNestedProperty_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectProperty.nestedProperty", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("nested value");
    }

    [Fact]
    public void TryExtractJsonValue_WithNestedWildcard_ReturnsFirstNestedProperty()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectProperty.*", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("nested value");
    }

    [Fact]
    public void TryExtractJsonValue_WithNonArrayAsArray_ReturnsFalse()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("stringProperty[0]", out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithNonExistentProperty_ReturnsFalse()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("nonExistentProperty", out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeEmpty();
    }

    [Fact]
    public void TryExtractJsonValue_WithNumericFilterExpression_ReturnsMatchingElement()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectArray[?(@.price>20)]", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldContain("Third Item");
    }

    [Fact]
    public void TryExtractJsonValue_WithObjectArrayProperty_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("objectArray[0].id", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("item1");
    }

    [Fact]
    public void TryExtractJsonValue_WithRecursiveDescent_FindsPropertyAtAnyLevel()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("$..duplicateProperty", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("top level");
    }

    [Fact]
    public void TryExtractJsonValue_WithRootArrayIndex_ReturnsValue()
    {
        // Arrange
        var arrayJson = """["first", "second", "third"]""";
        using var doc = JsonDocument.Parse(arrayJson);
        var element = doc.RootElement;

        // Act
        var success = element.TryExtractJsonValue("[1]", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("second");
    }

    // New tests for advanced JsonPath features

    [Fact]
    public void TryExtractJsonValue_WithRootIndicator_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("$.stringProperty", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("value");
    }

    [Fact]
    public void TryExtractJsonValue_WithScopedRecursiveDescent_FindsPropertyInScope()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("level1..duplicateProperty", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("level 1");
    }

    [Fact]
    public void TryExtractJsonValue_WithSimpleProperty_ReturnsValue()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("stringProperty", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("value");
    }

    [Fact]
    public void TryExtractJsonValue_WithWildcard_ReturnsFirstProperty()
    {
        // Arrange
        var element = _testDocument.RootElement;

        // Act
        var success = element.TryExtractJsonValue("*", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldNotBeEmpty();
    }
}