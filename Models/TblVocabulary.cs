using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblVocabulary
{
    public int VocabId { get; set; }

    public string Word { get; set; } = null!;

    public string? Meaning { get; set; }

    public string? ExampleEn { get; set; }

    public string? ExampleVi { get; set; }
}
