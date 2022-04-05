using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;
using HotChocolate.Language.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace GraphQL.Diff.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var schema = @"
type Foo { 
  bar: String 
  baz: [Int] 
}

type Query {
  fooList: [Foo!]
}

enum Episode {
  NEWHOPE
  EMPIRE
  JEDI
}

interface Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
}

type Human implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  starships: [Starship]
  totalCredits: Int
}


type Droid implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  primaryFunction: String
}

union SearchResult = Human | Droid

input ReviewInput {
  stars: Int!
  commentary: String
}

schema {
  query: Query
}
";
        var schemaTwo = @"

type Query {
  fooList: [Foo!]
}


        type Foo { 
          bar: String 
          baz: [Int]
        }

enum Episode {
  NEWHOPE
  EMPIRE
  JEDI
}

interface Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
}

type Human implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  starships: [Starship]
  totalCredits: Int
}


type Droid implements Character {
  id: ID!
  appearsIn: [Episode]!
  name: String!
  friends: [Character]
  primaryFunction: String
}

union SearchResult = Human | Droid

input ReviewInput {
  stars: Int!
  commentary: String
}

schema {
  query: Query
}
";
        DocumentNode document = Utf8GraphQLParser.Parse(schema);
        DocumentNode documentTwo = Utf8GraphQLParser.Parse(schemaTwo);

        var documentDefinitions = document.Definitions.ToList();
        var documentTwoDefinitions = documentTwo.Definitions.ToList();
        
        var incompatibleNodes = documentDefinitions
          .Where(d => d is not IHasName && d is not SchemaDefinitionNode)
          .ToList();
        
        var incompatibleNodesTwo = documentTwoDefinitions
          .Where(d => d is not IHasName && d is not SchemaDefinitionNode)
          .ToList();
        
        Assert.Empty(incompatibleNodes);
        Assert.Empty(incompatibleNodesTwo);

        Dictionary<string, DiffSet> emptyCompareList = new();
        var newCompareDictionary = AddValuesToCompareDictionary(documentDefinitions, emptyCompareList);
        var finalCompareDictionary = AddValuesToCompareDictionary(documentTwoDefinitions, newCompareDictionary);


        foreach (var (_, diffSet) in finalCompareDictionary)
        {
          Assert.Equal(diffSet.Expected, diffSet.Actual);
        }
        
        // todo: perhapse I better understand where given Kind can exist, then make a record structure
        // that maps the entire shape, then I can use any odd Object comparison library to return a diff
        // OR I can recursively iterate down and make my own diff tool with schema aware printing
    }

    private static Dictionary<string, DiffSet> AddValuesToCompareDictionary(List<IDefinitionNode> documentDefinitions, Dictionary<string, DiffSet> emptyCompareList)
    {
      var newCompareList = new Dictionary<string, DiffSet>(documentDefinitions.Select(doc =>
      {
        var print = doc.ToString(false);
        if (doc is IHasName namedDocument)
        {
          var name = namedDocument.Name.Value;
          return GetNewDiffSet(emptyCompareList, name, print);
        }

        if (doc is SchemaDefinitionNode)
        {
          return GetNewDiffSet(emptyCompareList, "schema", print);
        }

        throw new NotSupportedException("only supports schema and IHasName Definitions");
      }));
      return newCompareList;
    }

    private static KeyValuePair<string, DiffSet> GetNewDiffSet(Dictionary<string, DiffSet> compareList, string name, string print)
    {
      compareList.TryGetValue(name, out var maybeDiffSet);
      var dictionaryItem = maybeDiffSet ?? new DiffSet();

      if (dictionaryItem.Expected == null)
      {
        return new KeyValuePair<string, DiffSet>(name,
          dictionaryItem with { Expected = print });
      }

      return new KeyValuePair<string, DiffSet>(name,
        dictionaryItem with { Actual = print });
    }

    public record DiffSet(string? Expected, string? Actual)
    {
      public DiffSet() : this(null, null)
      {
      }
    }
}