using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class MenuCsvTests
{
    private const string Header = "Kategori;Ürün;Fiyat;Açıklama;Bölüm;KDV\n";

    [Fact]
    public void Parses_Semicolon_File_With_Turkish_Decimals_And_Quotes()
    {
        var result = MenuCsv.Parse("﻿" + Header + "Kebaplar;Adana Kebap;450,50;\"Acılı; közlenmiş biberle\";Mutfak;10\nİçecekler;Ayran;40;;Bar;\n");
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);
        var adana = result.Rows[0];
        Assert.Equal(("Kebaplar", "Adana Kebap", 450.50m, "Acılı; közlenmiş biberle", MenuDepartment.Mutfak, (int?)10),
            (adana.Category, adana.Name, adana.Price, adana.Description, adana.Department, adana.VatRate));
        Assert.Equal(MenuDepartment.Bar, result.Rows[1].Department);
        Assert.Null(result.Rows[1].VatRate);
        Assert.Null(result.Rows[1].Description);
    }

    [Fact]
    public void Parses_Comma_File_With_Dot_Decimals()
    {
        var result = MenuCsv.Parse("Kategori,Ürün,Fiyat,Açıklama,Bölüm,KDV\r\nTatlılar,Künefe,180.00,,Mutfak,10\r\n");
        Assert.Empty(result.Errors);
        Assert.Equal(180m, result.Rows.Single().Price);
    }

    [Fact]
    public void Reports_Every_Bad_Row_With_Line_Number()
    {
        var result = MenuCsv.Parse(Header + ";Adana;100;;;\nKebap;;100;;;\nKebap;Urfa;abc;;;\nKebap;Beyti;-5;;;\nKebap;Ali;10;;Salon;\nKebap;Veli;10;;;7\n");
        Assert.Empty(result.Rows);
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, result.Errors.Select(e => e.Line));
    }

    [Fact]
    public void Duplicate_Rows_And_Missing_Header_Are_Errors()
    {
        Assert.Single(MenuCsv.Parse(Header + "Kebap;Adana;100;;;\nkebap;ADANA;120;;;\n").Errors);
        Assert.NotEmpty(MenuCsv.Parse("a;b;c\nKebap;Adana;100\n").Errors);
        Assert.NotEmpty(MenuCsv.Parse("").Errors);
    }

    [Fact]
    public void Template_Round_Trips()
    {
        var result = MenuCsv.Parse(MenuCsv.Template);
        Assert.Empty(result.Errors);
        Assert.NotEmpty(result.Rows);
    }
}
