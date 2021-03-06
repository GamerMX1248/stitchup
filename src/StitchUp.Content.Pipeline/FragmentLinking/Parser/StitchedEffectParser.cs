using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content.Pipeline;
using StitchUp.Content.Pipeline.FragmentLinking.CodeModel;
using StitchUp.Content.Pipeline.Properties;

namespace StitchUp.Content.Pipeline.FragmentLinking.Parser
{
	public class StitchedEffectParser : Parser
	{
		private readonly ContentIdentity _identity;

		public StitchedEffectParser(string path, Token[] tokens, ContentIdentity identity)
			: base(path, tokens)
		{
			_identity = identity;
		}

		public StitchedEffectNode Parse()
		{
			TokenIndex = 0;

			return ParseStitchedEffectDeclaration();
		}

		private StitchedEffectNode ParseStitchedEffectDeclaration()
		{
			IdentifierToken fragmentName = ParseFileDeclaration(TokenType.StitchedEffect);

			List<ParseNode> blocks = ParseBlocks();

			return new StitchedEffectNode
			{
				Name = fragmentName.Identifier,
				Fragments = blocks.OfType<FragmentBlockNode>().FirstOrDefault(),
				Techniques = blocks.OfType<TechniqueBlockNode>().FirstOrDefault(),
				HeaderCode = blocks.OfType<HeaderCodeBlockNode>().FirstOrDefault(),
			};
		}

		protected override Func<ParseNode> GetBlockParseMethod(IdentifierToken blockName)
		{
			switch (blockName.Identifier)
			{
				case "techniques":
					return () => ParseTechniqueBlock();
				case "fragments":
					return () => ParseFragmentsBlock();
				default:
					return base.GetBlockParseMethod(blockName);
			}
		}

		private ParseNode ParseTechniqueBlock()
		{
			Eat(TokenType.CloseSquare);

			List<TechniqueNode> techniques = new List<TechniqueNode>();
			while (PeekType() == TokenType.Technique)
				techniques.Add(ParseTechnique());

			return new TechniqueBlockNode
			{
				Techniques = techniques
			};
		}

		private TechniqueNode ParseTechnique()
		{
			Eat(TokenType.Technique);

			IdentifierToken techniqueIdentifier = (IdentifierToken) Eat(TokenType.Identifier);

			Eat(TokenType.OpenCurly);

			List<TechniquePassNode> passes = new List<TechniquePassNode>();
			while (PeekType() == TokenType.Pass)
				passes.Add(ParseTechniquePass());

			Eat(TokenType.CloseCurly);

			return new TechniqueNode
			{
				Name = techniqueIdentifier.Identifier,
				Passes = passes
			};
		}

		private TechniquePassNode ParseTechniquePass()
		{
			Eat(TokenType.Pass);

			IdentifierToken passIdentifier = (IdentifierToken)Eat(TokenType.Identifier);

			Eat(TokenType.OpenCurly);

			IdentifierToken fragmentsIdentifier = (IdentifierToken) Eat(TokenType.Identifier);
			if (fragmentsIdentifier.Identifier != "fragments")
			{
				ReportError(Resources.ParserTokenExpected, "fragments");
				throw new NotSupportedException();
			}

			Eat(TokenType.Equal);

			Eat(TokenType.OpenSquare);
			List<Token> fragments = new List<Token>();
			while (PeekType() != TokenType.CloseSquare)
			{
				switch (PeekType())
				{
					case TokenType.Identifier:
						IdentifierToken fragmentIdentifier = (IdentifierToken) Eat(TokenType.Identifier);
						fragments.Add(fragmentIdentifier);
						break;
					case TokenType.Literal :
						LiteralToken fragmentString = (LiteralToken)Eat(TokenType.Literal);
						if (fragmentString.LiteralType != LiteralTokenType.String)
						{
							ReportError(Resources.ParserTokenUnexpected, fragmentString.ToString());
							throw new NotSupportedException();
						}
						fragments.Add(fragmentString);
						break;
					default :
						ReportError(Resources.StitchedEffectParserStringLiteralOrIdentifierExpected);
						throw new NotSupportedException();
				}

				if (PeekType() != TokenType.CloseSquare)
					Eat(TokenType.Comma);
			}

			Eat(TokenType.CloseSquare);
			Eat(TokenType.Semicolon);
			Eat(TokenType.CloseCurly);

			return new TechniquePassNode
			{
				Name = passIdentifier.Identifier,
				Fragments = fragments
			};
		}

		private ParseNode ParseFragmentsBlock()
		{
			Eat(TokenType.CloseSquare);

			List<VariableDeclarationNode> variableDeclarations = ParseVariableDeclarations(true, true, false, false, TokenType.Fragment);
			Dictionary<string, FragmentSource> fragmentDeclarations = new Dictionary<string, FragmentSource>();
			foreach (VariableDeclarationNode variableDeclaration in variableDeclarations)
			{
				string path = variableDeclaration.InitialValue.Trim('"');
				fragmentDeclarations.Add(variableDeclaration.Name, GetFragmentSource(path, _identity));
			}
			
			return new FragmentBlockNode
			{
				FragmentDeclarations = fragmentDeclarations
			};
		}

		protected override ParameterBlockType GetParameterBlockType(IdentifierToken blockName)
		{
			ReportError(Resources.StitchedEffectParserParameterBlockTypeExpected, blockName.Identifier);
			throw new NotSupportedException();
		}

		internal static FragmentSource GetFragmentSource(string path, ContentIdentity identity)
		{
			const string prefix = "library:";
			if (path.StartsWith(prefix))
			{
				string resourceName = path.Substring(prefix.Length);
				return new EmbeddedFragmentSource("Library." + resourceName + ".fragment");
			}
			return new FileFragmentSource(path, identity);
		}
	}
}