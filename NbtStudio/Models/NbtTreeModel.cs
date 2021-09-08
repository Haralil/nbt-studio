﻿using Aga.Controls.Tree;
using fNbt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NbtStudio.UI;
using System.Collections.ObjectModel;

namespace NbtStudio
{
    // the model version of the tree of nodes
    // this is mostly necessary because TreeViewAdv requires it, but it has some extra stuff as well
    public partial class NbtTreeModel : ITreeModel
    {
        public event EventHandler<TreeModelEventArgs> NodesChanged;
        public event EventHandler<TreeModelEventArgs> NodesInserted;
        public event EventHandler<TreeModelEventArgs> NodesRemoved;
        public event EventHandler<TreePathEventArgs> StructureChanged;

        private readonly List<Node> Roots = new();
        public ReadOnlyCollection<Node> RootNodes => Roots.AsReadOnly();

        public bool HasUnsavedChanges => GetFiles().Any(x => x.GetFile().HasUnsavedChanges);
        public IEnumerable<Node> GetFiles()
        {
            return BreadthFirstSearch(x => x is FolderNode || x.GetFile() is not null);
        }

        public void Replace(params IHavePath[] paths)
        {
            if (Roots.Count > 0)
            {
                NodesRemoved?.Invoke(this, new TreeModelEventArgs(TreePath.Empty, Enumerable.Range(0, Roots.Count).ToArray(), Roots.ToArray()));
                UnsubscribeNodeEvents(Roots);
                Roots.Clear();
            }
            Import(paths);
        }

        public void Import(params IHavePath[] paths)
        {
            ImportNodes(paths.Select(MakeNode).ToArray());
        }

        public void ImportNodes(params Node[] nodes)
        {
            if (nodes.Any(x => x.Parent is not null))
                throw new InvalidOperationException($"One or more specified nodes already have a parent.");
            int roots_length = Roots.Count;
            Roots.AddRange(nodes);
            SubscribeNodeEvents(nodes);
            NodesInserted?.Invoke(this, new TreeModelEventArgs(TreePath.Empty, Enumerable.Range(roots_length, nodes.Length).ToArray(), nodes));
        }

        private void SubscribeNodeEvents(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
            {
                node.SomethingChanged += Node_SomethingChanged;
            }
        }

        private void UnsubscribeNodeEvents(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
            {
                node.SomethingChanged -= Node_SomethingChanged;
            }
        }

        // to do: ehh...
        public void Refresh()
        {
            foreach (var node in Node.DirtyNodes)
            {
                Node_SomethingChanged(node.RefreshChildren());
            }
        }

        private void Node_SomethingChanged(ChildrenChangedReport report)
        {
            var removed = report.RemovedArgs();
            if (removed is not null)
                NodesRemoved?.Invoke(this, removed);
            var added = report.InsertedArgs();
            if (added is not null)
                NodesInserted?.Invoke(this, added);
            var structure = report.StructureChangedArgs();
            if (structure is not null)
                StructureChanged?.Invoke(this, structure);
            var changed = report.ChangedArgs();
            if (changed is not null)
                NodesChanged?.Invoke(this, changed);
        }

        private Node MakeNode(IHavePath item)
        {
            if (item is NbtFile file)
                return new NbtFileNode(file);
            if (item is RegionFile region)
                return new RegionFileNode(region);
            if (item is NbtFolder folder)
                return new FolderNode(folder);
            throw new ArgumentException();
        }

        private IEnumerable<Node> BreadthFirstSearch(Predicate<Node> predicate)
        {
            var queue = new Queue<Node>(Roots);
            while (queue.Any())
            {
                var item = queue.Dequeue();
                if (!predicate(item))
                    continue;
                yield return item;
                foreach (var sub in item.Children)
                {
                    queue.Enqueue(sub);
                }
            }
        }

        public (Node destination, int index) GetInsertionLocation(Node target, NodePosition position)
        {
            if (position == NodePosition.Inside)
                return (target, target.Children.Count);
            else
            {
                var parent = target.Parent;
                var siblings = parent.Children.ToList();
                int index = siblings.IndexOf(target);
                if (position == NodePosition.After)
                    index++;
                return (parent, index);
            }
        }

        IEnumerable ITreeModel.GetChildren(TreePath treePath) => GetChildren(treePath);
        public IEnumerable<object> GetChildren(TreePath treePath)
        {
            if (treePath.IsEmpty())
                return Roots;
            else
                return ((Node)treePath.LastNode).Children;
        }

        public bool IsLeaf(TreePath treePath)
        {
            return !((Node)treePath.LastNode).HasChildren;
        }
    }
}
