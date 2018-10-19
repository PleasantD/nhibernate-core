﻿using System.Collections.Generic;
using System.Linq.Expressions;
using NHibernate.Linq.Clauses;
using NHibernate.Linq.Visitors;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing;

namespace NHibernate.Linq.ReWriters
{
	/// <summary>
	/// Expands conditionals within subquery FROM clauses.
	/// It does this by moving the conditional expression outside of the subquery and cloning the subquery,
	/// replacing the FROM clause with the collection parts of the conditional.
	/// </summary>
	internal class SubQueryConditionalExpander : NhQueryModelVisitorBase
	{
		private readonly SubQueryConditionalExpressionExpander _expander = new SubQueryConditionalExpressionExpander();

		public override void VisitSelectClause(SelectClause selectClause, QueryModel queryModel)
		{
			_expander.Transform(selectClause);
		}

		public override void VisitOrdering(Ordering ordering, QueryModel queryModel, OrderByClause orderByClause, int index)
		{
			_expander.Transform(ordering);
		}

		public override void VisitResultOperator(ResultOperatorBase resultOperator, QueryModel queryModel, int index)
		{
			_expander.Transform(resultOperator);
		}

		public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
		{
			_expander.Transform(whereClause);
		}

		public override void VisitNhHavingClause(NhHavingClause havingClause, QueryModel queryModel, int index)
		{
			_expander.Transform(havingClause);
		}

		public static void ReWrite(QueryModel queryModel)
		{
			var visitor = new SubQueryConditionalExpander();
			visitor.VisitQueryModel(queryModel);
		}

		private class SubQueryConditionalExpressionExpander : RelinqExpressionVisitor
		{
			public void Transform(IClause clause)
			{
				clause.TransformExpressions(Visit);
			}

			public void Transform(Ordering ordering)
			{
				ordering.TransformExpressions(Visit);
			}

			public void Transform(ResultOperatorBase resultOperator)
			{
				resultOperator.TransformExpressions(Visit);
			}

			protected override Expression VisitSubQuery(SubQueryExpression expression)
			{
				var fromClauseExpander = new SubQueryFromClauseExpander(expression.QueryModel);
				var fromExpr = fromClauseExpander.Visit(expression.QueryModel.MainFromClause.FromExpression);
				return fromClauseExpander.Rewritten ? fromExpr : expression;
			}
		}

		private class SubQueryFromClauseExpander : RelinqExpressionVisitor
		{
			private readonly QueryModel _originalSubQueryModel;
			private int _depth = -1;
			private readonly IList<bool> _nominate = new List<bool>();

			public bool Rewritten { get; private set; }

			public SubQueryFromClauseExpander(QueryModel originalSubQueryModel)
			{
				_originalSubQueryModel = originalSubQueryModel;
			}

			protected override Expression VisitConditional(ConditionalExpression node)
			{
				if (_depth >= 0)
				{
					_nominate[_depth] = false;
				}

				var newTest = this.Visit(node.Test);
				_nominate.Insert(++_depth, false);
				var newTrue = this.Visit(node.IfTrue);
				if (_nominate[_depth])
				{
					newTrue = BuildNewSubQuery(newTrue);
					Rewritten = true;
				}
				_nominate.Insert(_depth, false);
				var newFalse = this.Visit(node.IfFalse);
				if (_nominate[_depth])
				{
					newFalse = BuildNewSubQuery(newFalse);
					Rewritten = true;
				}
				_nominate.RemoveAt(_depth--);
				return Expression.Condition(newTest, newTrue, newFalse);
			}

			protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression expression)
			{
				if (_depth >= 0)
				{
					_nominate[_depth] = true;
				}

				return base.VisitQuerySourceReference(expression);
			}

			private SubQueryExpression BuildNewSubQuery(Expression fromExpr)
			{
				var newSubQuery = _originalSubQueryModel.Clone();
				newSubQuery.MainFromClause.FromExpression = fromExpr;
				return new SubQueryExpression(newSubQuery);
			}
		}
	}
}
