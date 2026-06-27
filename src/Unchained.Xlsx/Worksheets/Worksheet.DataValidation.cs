using System.Globalization;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Xlsx.Core.Xml;
using Unchained.Xlsx.DataValidation;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.DataValidation;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    private readonly DataValidationCollection _dataValidations = new();
    private bool _dataValidationsParsed;

    /// <summary>The data-validation rules defined on this worksheet.</summary>
    public DataValidationCollection DataValidations
    {
        get
        {
            EnsureDataValidationsParsed();
            return _dataValidations;
        }
    }

    /// <summary>Adds a drop-down list validation to <paramref name="range" /> from an explicit list of options.</summary>
    public DataValidation.DataValidation AddDropdownValidation(CellRange range, params string[] options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validation = new DataValidation.DataValidation
        {
            Type = DataValidationType.List,
            Formula1 = "\"" + string.Join(',', options) + "\""
        };
        validation.Ranges.Add(range);
        return DataValidations.Add(validation);
    }

    internal bool DataValidationsMaterialised => _dataValidationsParsed;

    internal DataValidationCollection DataValidationsInternal => _dataValidations;

    private void EnsureDataValidationsParsed()
    {
        if (_dataValidationsParsed)
            return;

        _dataValidationsParsed = true;
        var container = RawElement?.Child(SmlNames.DataValidations);
        if (container == null)
            return;

        foreach (var element in container.Children(SmlNames.DataValidation))
            _dataValidations.AddExisting(ReadValidation(element));
    }

    private static DataValidation.DataValidation ReadValidation(XElement element)
    {
        var validation = new DataValidation.DataValidation
        {
            Type = SmlEnums.ParseValidationType(element.GetAttr("type")),
            Operator = SmlEnums.ParseValidationOperator(element.GetAttr("operator")),
            AllowBlank = element.GetAttrBool("allowBlank") == true,
            ShowInputMessage = element.GetAttrBool("showInputMessage") == true,
            ShowErrorAlert = element.GetAttrBool("showErrorAlert") == true,
            ShowDropDown = element.GetAttrBool("showDropDown") != true, // attribute means "hide" in OOXML
            ErrorStyle = SmlEnums.ParseErrorStyle(element.GetAttr("errorStyle")),
            ErrorTitle = element.GetAttr(SmlNames.AttributeErrorTitle),
            ErrorMessage = element.GetAttr(SmlNames.AttributeErrorMessage),
            PromptTitle = element.GetAttr(SmlNames.AttributePromptTitle),
            Prompt = element.GetAttr(SmlNames.AttributePrompt),
            Formula1 = element.Child(SmlNames.Formula1)?.Value,
            Formula2 = element.Child(SmlNames.Formula2)?.Value
        };

        foreach (var token in (element.GetAttr("sqref") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            validation.Ranges.Add(CellRange.FromA1(token));

        return validation;
    }
}
