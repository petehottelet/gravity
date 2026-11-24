using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed class SearchPanel : UserControl
    {
        private readonly Label status;
        private readonly ListBox matches;
        private readonly Button cancel;
        private readonly TextBox query;
        private readonly CheckBox matchCase;
        private CancellationTokenSource running;
        private SearchResults results;
        private int generation;
        public event Action<SearchMatch> MatchSelected;
        public event Action CloseRequested;
        public event Action QueryChanged;
        public event Action CaseChanged;
        public event Action<bool> NextRequested;
        public string Query { get { return query.Text; } set { query.Text = value; } }
        public bool MatchCase { get { return matchCase.Checked; } set { matchCase.Checked = value; } }
        public bool QueryFocused { get { return query.Focused; } }
        public bool Searching { get { return running != null; } }
        public int MatchCount { get { return results == null ? 0 : results.Matches.Count; } }
        public bool Complete { get; private set; }

        public SearchPanel()
        {
            BackColor = Color.White;
            Padding = new Padding(8);
            AccessibleName = "Find results";
            var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 32, ColumnCount = 3 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Text = "Type in Find to search this document." };
            cancel = new Button { Text = "Cancel", Dock = DockStyle.Fill, Enabled = false };
            var close = new Button { Text = "Close", Dock = DockStyle.Fill };
            header.Controls.Add(status, 0, 0); header.Controls.Add(cancel, 1, 0); header.Controls.Add(close, 2, 0);
            matches = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = "Search matches" };
            var input = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 5 };
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            query = new TextBox { Dock = DockStyle.Fill, AccessibleName = "Find text" };
            matchCase = new CheckBox { Text = "Match case", Dock = DockStyle.Fill };
            var previous = new Button { Text = "Previous", Dock = DockStyle.Fill };
            var next = new Button { Text = "Next", Dock = DockStyle.Fill };
            input.Controls.Add(new Label { Text = "Find", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            input.Controls.Add(query, 1, 0); input.Controls.Add(matchCase, 2, 0); input.Controls.Add(previous, 3, 0); input.Controls.Add(next, 4, 0);
            Controls.Add(matches); Controls.Add(header); Controls.Add(input);
            query.TextChanged += delegate { if (QueryChanged != null) QueryChanged(); };
            matchCase.CheckedChanged += delegate { if (CaseChanged != null) CaseChanged(); };
            previous.Click += delegate { if (NextRequested != null) NextRequested(false); };
            next.Click += delegate { if (NextRequested != null) NextRequested(true); };
            query.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { if (NextRequested != null) NextRequested(!e.Shift); e.SuppressKeyPress = true; } };
            cancel.Click += delegate { Cancel(); status.Text = "Search cancelled. Press Enter in Find to search again."; };
            close.Click += delegate { var handler = CloseRequested; if (handler != null) handler(); };
            matches.SelectedIndexChanged += delegate
            {
                var match = matches.SelectedItem as SearchMatch;
                if (match != null && MatchSelected != null) MatchSelected(match);
                if (match != null && results != null) status.Text = Summary(matches.SelectedIndex + 1);
            };
        }

        public void FocusQuery(bool selectAll = true)
        {
            query.Focus();
            if (selectAll) query.SelectAll();
            else { query.SelectionStart = query.TextLength; query.SelectionLength = 0; }
        }

        private string Summary(int selected)
        {
            string text = (selected > 0 ? selected + " of " : "") + results.Total + (results.Total == 1 ? " match" : " matches");
            if (results.Total > SearchResults.DisplayLimit) text += " · showing first " + SearchResults.DisplayLimit + "; refine your search";
            return text;
        }

        public void Cancel()
        {
            generation++;
            if (running != null) { running.Cancel(); running = null; }
            cancel.Enabled = false;
        }

        public void Reset()
        {
            Cancel(); Complete = false; results = null; matches.Items.Clear();
            status.Text = "Type in Find to search this document.";
        }

        public void Search(IList<PageRef> pages, string query, bool matchCase, int startPage, bool forward)
        {
            Reset();
            if (string.IsNullOrEmpty(query)) return;
            var snapshot = new List<PageRef>(pages);
            var lease = PdfSource.Retain(snapshot);
            var cts = new CancellationTokenSource(); running = cts;
            int stamp = generation;
            cancel.Enabled = true; status.Text = "Searching " + snapshot.Count + " pages…";
            Task.Factory.StartNew(() =>
            {
                using (lease)
                {
                    var clock = Stopwatch.StartNew(); long last = 0;
                    return DocumentSearch.Find(snapshot, query, matchCase, cts.Token, (page, found) =>
                    {
                        if (clock.ElapsedMilliseconds - last < 150) return;
                        last = clock.ElapsedMilliseconds;
                        Post(() => { if (stamp == generation) status.Text = "Searching " + page + " of " + snapshot.Count + " pages · " + found + " matches"; });
                    });
                }
            }).ContinueWith(task =>
            {
                // Observe failures even when a newer search has replaced this one.
                Exception error = task.IsFaulted ? task.Exception.GetBaseException() : null;
                bool stopped = cts.IsCancellationRequested;
                bool posted = Post(() =>
                {
                    try
                    {
                        if (stamp != generation) return;
                        running = null; cancel.Enabled = false;
                        if (stopped || error is OperationCanceledException) { status.Text = "Search cancelled."; return; }
                        if (error != null) { status.Text = "Search failed. Try reopening the document."; return; }
                        results = task.Result; Complete = true;
                        matches.BeginUpdate();
                        matches.Items.AddRange(results.Matches.ToArray());
                        matches.EndUpdate();
                        status.Text = results.Total == 0 ? "No matches. Try different text or turn off Match case." : Summary(0);
                        if (matches.Items.Count > 0)
                        {
                            int at = forward ? results.Matches.FindIndex(m => m.Page >= startPage) : results.Matches.FindLastIndex(m => m.Page <= startPage);
                            matches.SelectedIndex = at < 0 ? (forward ? 0 : matches.Items.Count - 1) : at;
                        }
                    }
                    finally { cts.Dispose(); }
                });
                if (!posted) cts.Dispose();
            }, TaskScheduler.Default);
        }

        private bool Post(Action action)
        {
            try { if (!IsDisposed && IsHandleCreated) { BeginInvoke(action); return true; } }
            catch (InvalidOperationException) { }
            return false;
        }

        public void Next(bool forward)
        {
            if (matches.Items.Count == 0) return;
            int at = matches.SelectedIndex;
            matches.SelectedIndex = (at + (forward ? 1 : -1) + matches.Items.Count) % matches.Items.Count;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Cancel();
            base.Dispose(disposing);
        }
    }
}
