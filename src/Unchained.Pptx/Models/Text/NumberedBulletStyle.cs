namespace Unchained.Pptx.Models.Text;

/// <summary>The numbering sequence style for an auto-numbered bullet.</summary>
public enum NumberedBulletStyle
{
    /// <summary>Arabic numerals: 1, 2, 3 …</summary>
    Arabic,
    /// <summary>Arabic numerals followed by a closing parenthesis: 1), 2), 3) …</summary>
    ArabicParenthesis,
    /// <summary>Arabic numerals followed by a period: 1. 2. 3. …</summary>
    ArabicPeriod,
    /// <summary>Uppercase Roman numerals: I, II, III …</summary>
    RomanUpperCase,
    /// <summary>Lowercase Roman numerals: i, ii, iii …</summary>
    RomanLowerCase,
    /// <summary>Uppercase Latin letters: A, B, C …</summary>
    LetterUpperCase,
    /// <summary>Lowercase Latin letters: a, b, c …</summary>
    LetterLowerCase,
    /// <summary>Uppercase Latin letters followed by a period: A. B. C. …</summary>
    LetterUpperCasePeriod,
    /// <summary>Lowercase Latin letters followed by a period: a. b. c. …</summary>
    LetterLowerCasePeriod
}
