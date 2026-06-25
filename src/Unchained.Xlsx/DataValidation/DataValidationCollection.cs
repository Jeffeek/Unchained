using System.Collections;

namespace Unchained.Xlsx.DataValidation;

/// <summary>The data-validation rules defined on a worksheet.</summary>
public sealed class DataValidationCollection : IReadOnlyList<DataValidation>
{
    private readonly List<DataValidation> _validations = [];

    /// <summary>The number of validation rules.</summary>
    public int Count => _validations.Count;

    /// <summary>Returns the validation at the given index.</summary>
    public DataValidation this[int index] => _validations[index];

    /// <inheritdoc />
    public IEnumerator<DataValidation> GetEnumerator() => _validations.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Adds a validation rule.</summary>
    public DataValidation Add(DataValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        _validations.Add(validation);
        return validation;
    }

    /// <summary>Removes a validation rule.</summary>
    public void Remove(DataValidation validation) => _validations.Remove(validation);

    internal void AddExisting(DataValidation validation) => _validations.Add(validation);
}
