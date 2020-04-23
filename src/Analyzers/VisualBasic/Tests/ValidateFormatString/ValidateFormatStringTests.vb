﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions
Imports Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Diagnostics
Imports Microsoft.CodeAnalysis.ValidateFormatString
Imports Microsoft.CodeAnalysis.VisualBasic.ValidateFormatString

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.ValidateFormatString
    Public Class ValidateFormatStringTests
        Inherits AbstractVisualBasicDiagnosticProviderBasedUserDiagnosticTest

        Friend Overrides Function CreateDiagnosticProviderAndFixer(
                workspace As Workspace) As (DiagnosticAnalyzer, CodeFixProvider)
            Return (New VisualBasicValidateFormatStringDiagnosticAnalyzer, Nothing)
        End Function

        Private Function OptionOn() As OptionsCollection
            Return New OptionsCollection(GetLanguage()) From {{ValidateFormatStringOption.ReportInvalidPlaceholdersInStringDotFormatCalls, True}}
        End Function

        Private Function OptionOff() As OptionsCollection
            Return New OptionsCollection(GetLanguage()) From {{ValidateFormatStringOption.ReportInvalidPlaceholdersInStringDotFormatCalls, False}}
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function ParamsObjectArray() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format(""This {0} {1} {[||]2} works"", New Object  { ""test"", ""test2"", ""test3"" })
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function TwoPlaceholders() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format(""This {0} {1[||]} works"", ""test"", ""also"")
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function IFormatProviderAndThreePlaceholders() As Task
            Await TestDiagnosticMissingAsync("
Imports System.Globalization
Class C
     Sub Main 
        Dim culture as CultureInfo = ""da - da""
        string.Format(culture, ""The current price is {0[||]:C2} per ounce"", 2.45)
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function OnePlaceholderOutOfBounds() As Task
            Await TestDiagnosticInfoAsync("
Class C
     Sub Main 
        string.Format(""This [|{1}|] is my test"", ""teststring1"")
    End Sub
End Class",
        options:=Nothing,
        diagnosticId:=IDEDiagnosticIds.ValidateFormatStringDiagnosticID,
        diagnosticSeverity:=DiagnosticSeverity.Warning,
        diagnosticMessage:=AnalyzersResources.Format_string_contains_invalid_placeholder)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function FourPlaceholdersWithOnePlaceholderOutOfBounds() As Task
            Await TestDiagnosticInfoAsync("
Class C
     Sub Main 
        string.Format(""This{0}{1}{2}{3}[|{4}|] is my test"", ""teststring1"", ""teststring2"", ""teststring3"", ""teststring4"")
    End Sub
End Class",
        options:=Nothing,
        diagnosticId:=IDEDiagnosticIds.ValidateFormatStringDiagnosticID,
        diagnosticSeverity:=DiagnosticSeverity.Warning,
        diagnosticMessage:=AnalyzersResources.Format_string_contains_invalid_placeholder)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function IFormatProviderAndTwoPlaceholdersWithOnePlaceholderOutOfBounds() As Task
            Await TestDiagnosticInfoAsync("
Imports System.Globalization
Class C
     Sub Main 
        Dim culture As CultureInfo = ""da - da""
        string.Format(culture, ""This [|{2}|] is my test"", ""teststring1"", ""teststring2"")
    End Sub
End Class",
        options:=Nothing,
        diagnosticId:=IDEDiagnosticIds.ValidateFormatStringDiagnosticID,
        diagnosticSeverity:=DiagnosticSeverity.Warning,
        diagnosticMessage:=AnalyzersResources.Format_string_contains_invalid_placeholder)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function NamedParameters() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format(arg0:= ""test"", arg1:= ""also"", format:= ""This {0[||]} {1} works"")
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function NamedParametersOneOutOfBounds() As Task
            Await TestDiagnosticInfoAsync("
Class C
     Sub Main 
        string.Format(arg0:= ""test"", arg1:= ""also"", format:= ""This {0} [|{2}|] works"")
    End Sub
End Class",
        options:=Nothing,
        diagnosticId:=IDEDiagnosticIds.ValidateFormatStringDiagnosticID,
        diagnosticSeverity:=DiagnosticSeverity.Warning,
        diagnosticMessage:=AnalyzersResources.Format_string_contains_invalid_placeholder)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function NamedParametersWithIFormatProvider() As Task
            Await TestDiagnosticMissingAsync("
Imports System.Globalization
Class C
     Sub Main 
        Dim culture As CultureInfo = ""da - da""
        string.Format(arg0:= 2.45, provider:=culture, format :=""The current price is {0[||]:C2} per ounce -no squiggles"")
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function NamedParametersWithIFormatProviderAndParamsObject() As Task
            Await TestDiagnosticMissingAsync("
Imports System.Globalization
Class C
     Sub Main 
        Dim culture As CultureInfo = ""da - da""
        string.Format(format:= ""This {0} {1[||]} {2} works"", args:=New Object  { ""test"", ""it"", ""really"" }, provider:=culture)
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function DuplicateNamedParameters() As Task
            Await TestDiagnosticMissingAsync("
Imports System.Globalization
Class C
     Sub Main 
        Dim culture As CultureInfo = ""da - da""
        string.Format(format:= ""This {0} {1[||]} {2} works"", format:=New Object  { ""test"", ""it"", ""really"" }, provider:=culture)
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function DuplicateNamedParametersInNet45() As Task
            Await TestDiagnosticMissingAsync("
<Workspace>
                             <Project Language=""Visual Basic"" CommonReferencesNet45=""true"">
                                 <Document FilePath=""SourceDocument"">
Imports System.Globalization
Class C
     Sub Main 
        Dim culture As CultureInfo = ""da - da""
        string.Format(format:= ""This {0} {1[||]} {2} works"", format:=New Object  { ""test"", ""it"", ""really"" }, provider:=culture)
    End Sub
End Class
        </Document>
            </Project>
                </Workspace>")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function NamespaceAliasForStringClass() As Task
            Await TestDiagnosticMissingAsync("
Imports stringalias = System.String
Class C
     Sub Main 
        stringAlias.Format(""This {[||]0} works"", ""test"")
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function VerbatimMultipleLines() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format(@""This {[||]0} 
{1} {2} works"", ""multiple"", ""line"", ""test""))
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function Interpolated() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format($""This {0} 
{1[||]} {2} works"", ""multiple"", ""line"", ""test""))
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function Empty() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format(""[||]"")
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function LeftParenOnly() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format([||]
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function ParenthesesOnly() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Format [||]
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function DifferentFunction() As Task
            Await TestDiagnosticMissingAsync("
Class C
     Sub Main 
        string.Compare [||]
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function FormatMethodOnGenericIdentifier() As Task
            Await TestDiagnosticMissingAsync("
Class G(Of T)
    Function Format(Of T)(goo as String)
        Return True
    End Function
End Class

Class C
    Sub Goo()
        Dim q As G(Of Integer)
        q.Format(Of Integer)(""TestStr[||]ing"")
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function OmittedArgument() As Task
            Await TestDiagnosticMissingAsync("Module M
    Sub Main()
         String.Format([||],)
    End Sub
End Module")
        End Function

#If CODE_STYLE Then
        <InlineData(False, True)>   ' Option has no effect on CodeStyle layer CI execution as it is not an editorconfig option.
        <InlineData(True, True)>
        <Theory, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function TestOption(optionOn As Boolean, expectDiagnostic As Boolean) As Task
#Else
        <InlineData(False, False)>
        <InlineData(True, True)>
        <Theory, Trait(Traits.Feature, Traits.Features.ValidateFormatString)>
        Public Async Function TestOption(optionOn As Boolean, expectDiagnostic As Boolean) As Task
#End If
            Dim source = "
Class C
     Sub Main 
        string.Format(""This {0} [|{2}|] works"", ""test"", ""also"")
    End Sub
End Class"
            Dim options = If(optionOn, Me.OptionOn(), OptionOff())
            If Not expectDiagnostic Then
                Await TestDiagnosticMissingAsync(source, New TestParameters(options:=options))
            Else
                Await TestDiagnosticInfoAsync(source,
                    options,
                    diagnosticId:=IDEDiagnosticIds.ValidateFormatStringDiagnosticID,
                    diagnosticSeverity:=DiagnosticSeverity.Warning,
                    diagnosticMessage:=AnalyzersResources.Format_string_contains_invalid_placeholder)
            End If
        End Function
    End Class
End Namespace
