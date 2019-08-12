using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class TreeNode
{
	public TreeNode Parent;
	public TreeNode FirstChild;
	public TreeNode PrevSibling, NextSibling;

	public void Remove()
	{
		if (Parent != null)
		{
			if (Parent.FirstChild == this)
			{
				Parent.FirstChild = this.NextSibling;
			}

			if (PrevSibling != null)
			{
				PrevSibling.NextSibling = NextSibling;
			}

			if (NextSibling != null)
			{
				NextSibling.PrevSibling = PrevSibling;
			}

			NextSibling = PrevSibling = null;

			Parent = null;
		}
	}

	public void AddChild(TreeNode node)
	{
		if (FirstChild == null)
		{
			FirstChild = node;
		}
		else
		{
			var t = FirstChild.NextSibling;
			FirstChild.NextSibling = node;
			node.PrevSibling = FirstChild;
			node.NextSibling = t;
		}

		node.Parent = this;
	}
}

/*public class LinkedTree<T> where T : LinkedTree<T>.Node
{
	public abstract class Node
	{
		public Node Parent;
		public Node FirstChild;
		public Node PrevSibling, NextSibling;
	}
}*/
