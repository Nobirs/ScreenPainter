using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLab1
{
    public class DrawingContext
    {
        public List<IDrawable> Shapes { get; } = new List<IDrawable>();
        public DrawingTool CurrentTool { get; set; }

        private readonly Stack<IDrawable> _undoStack = new Stack<IDrawable>();
        private readonly Stack<IDrawable> _redoStack = new Stack<IDrawable>();

        public void CommitShape(IDrawable shape)
        {
            if (shape == null) return;
            Shapes.Add(shape);
            _undoStack.Push(shape);
            _redoStack.Clear();
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Undo()
        {
            if (!CanUndo) return;
            var s = _undoStack.Pop();
            Shapes.Remove(s);
            _redoStack.Push(s);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var s = _redoStack.Pop();
            Shapes.Add(s);
            _undoStack.Push(s);
        }

        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
