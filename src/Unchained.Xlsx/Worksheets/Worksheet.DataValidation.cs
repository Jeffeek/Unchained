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

    internal static XElement WriteValidations(DataValidationCollection validations)
    {
        var container = new XElement(SmlNames.DataValidations,
            new XAttribute("count", validations.Count.ToString(CultureInfo.InvariantCulture)));

        foreach (var validation in validations)
            container.Add(WriteValidation(validation));

        return container;
    }

    private static XElement WriteValidation(DataValidation.DataValidation validation)
    {
        var element = new XElement(SmlNames.DataValidation);

        var type = SmlEnums.ToLiteral(validation.Type);
        if (type != null) element.SetAttributeValue("type", type);

        var op = SmlEnums.ToLiteral(validation.Operator);
        if (op != null) element.SetAttributeValue("operator", op);

        if (validation.AllowBlank) element.SetAttributeValue("allowBlank", "1");
        if (validation.ShowInputMessage) element.SetAttributeValue("showInputMessage", "1");
        if (validation.ShowErrorAlert) element.SetAttributeValue("showErrorAlert", "1");
        if (!validation.ShowDropDown) element.SetAttributeValue("showDropDown", "1");

        var errorStyle = SmlEnums.ToLiteral(validation.ErrorStyle);
        if (errorStyle != null) element.SetAttributeValue("errorStyle", errorStyle);
        if (!string.IsNullOrEmpty(validation.ErrorTitle)) element.SetAttributeValue("errorTitle", validation.ErrorTitle);
        if (!string.IsNullOrEmpty(validation.ErrorMessage)) element.SetAttributeValue("error", validation.ErrorMessage);
        if (!string.IsNullOrEmpty(validation.PromptTitle)) element.SetAttributeValue("promptTitle", validation.PromptTitle);
        if (!string.IsNullOrEmpty(validation.Prompt)) element.SetAttributeValue("prompt", validation.Prompt);

        element.SetAttributeValue("sqref", string.Join(' ', validation.Ranges.Select(static r => r.ToA1())));

        if (validation.Formula1 != null)
            element.Add(new XElement(SmlNames.Formula1, validation.Formula1));
        if (validation.Formula2 != null)
            element.Add(new XElement(SmlNames.Formula2, validation.Formula2));

        return element;
    }
}
