using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EVEMon.Common;

namespace EVEMon.SkillPlanner
{
    /// <summary>
    /// Represents the diagram displayed on the skill browser.
    /// </summary>
    public partial class SkillTreeDisplayControl : UserControl
    {

        #region Fields

        private const int SkillboxMarginUd = 20;
        private const int SkillboxMarginLr = 10;

        private const DescriptiveTextOptions TimeFormat =
            DescriptiveTextOptions.UppercaseText | DescriptiveTextOptions.IncludeCommas;

        public event SkillClickedHandler SkillClicked;

        private Plan m_plan;
        private Skill m_rootSkill;
        private Cell m_rootCell;
        private Rectangle m_graphBounds = new Rectangle(0, 0, 10, 10);

        #endregion


        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public SkillTreeDisplayControl()
        {
            InitializeComponent();
        }

        #endregion


        #region Inherited Events

        /// <summary>
        /// Subscribe events on load.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.Opaque |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();

            EveMonClient.SettingsChanged += EveMonClient_SettingsChanged;
            EveMonClient.CharacterUpdated += EveMonClient_CharacterUpdated;
            EveMonClient.PlanChanged += EveMonClient_PlanChanged;
            Disposed += OnDisposed;
        }

        /// <summary>
        /// Unsubscribe events on disposing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDisposed(object sender, EventArgs e)
        {
            EveMonClient.SettingsChanged -= EveMonClient_SettingsChanged;
            EveMonClient.CharacterUpdated -= EveMonClient_CharacterUpdated;
            EveMonClient.PlanChanged -= EveMonClient_PlanChanged;
            Disposed -= OnDisposed;
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Gets the cell width according to dpi (for font scaling support).
        /// </summary>
        private int CellWidth
        {
            get
            {
                Graphics g = Graphics.FromHwnd(Handle);
                float dpi = g.DpiX;

                if (dpi > 125)
                    return 353;

                return dpi > 96 ? 295 : 235;
            }
        }

        /// <summary>
        /// Gets the cell height according to dpi (for font scaling support).
        /// </summary>
        private int CellHeight
        {
            get
            {
                Graphics g = Graphics.FromHwnd(Handle);
                float dpi = g.DpiX;

                if (dpi > 125)
                    return 110;

                return dpi > 96 ? 92 : 73;
            }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Gets or sets the plan this control is bound to.
        /// </summary>
        public Plan Plan
        {
            get { return m_plan; }
            set
            {
                m_plan = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the root skill.
        /// </summary>
        public Skill RootSkill
        {
            get { return m_rootSkill; }
            set
            {
                if (m_rootSkill == value)
                    return;

                m_rootSkill = value;
                UpdateLayout();
            }
        }

        #endregion


        #region Layout and painting

        /// <summary>
        /// Checks training data.
        /// </summary>
        private void CheckTraining()
        {
            if (m_rootSkill == null)
                return;

            tmrSkillTick.Enabled = m_rootSkill.AllPrerequisites.Any(x => x.Skill.IsTraining);
        }

        /// <summary>
        /// Build the layout.
        /// </summary>
        private void UpdateLayout()
        {
            if (m_rootSkill == null)
                return;

            m_rootCell = new Cell(m_rootSkill);
            ArrangeGraph();
        }

        /// <summary>
        /// Arranges the graph position.
        /// </summary>
        private void ArrangeGraph()
        {
            if (m_rootSkill == null)
                return;

            m_graphBounds = m_rootCell.Arrange(Size);
            AutoScrollMinSize = m_graphBounds.Size;
            Invalidate();
        }

        /// <summary>
        /// Performs the painting.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Draws the background (solid or gradient, depending on safe mode or not)
            if (!Settings.UI.SafeForWork)
            {
                using (Brush b = new LinearGradientBrush(ClientRectangle, Color.LightBlue, Color.DarkBlue, 90.0F))
                {
                    g.FillRectangle(b, e.ClipRectangle);
                }
            }
            else
            {
                g.FillRectangle(SystemBrushes.Control, e.ClipRectangle);
            }

            // Returns when no root skill
            if (m_rootSkill == null)
                return;

            // Compute offset caused by scrollers
            int ofsLeft = AutoScrollPosition.X;
            int ofsTop = AutoScrollPosition.Y;

            // Draw the lines
            using (Pen linePen = new Pen((Settings.UI.SafeForWork ? SystemColors.ControlText : Color.White), 5.0F))
            {
                foreach (Cell cell in m_rootCell.m_cells)
                {
                    DrawLines(g, m_rootCell, cell, linePen, ofsLeft, ofsTop);
                }
            }

            // Draw the cells
            using (Font boldFont = FontFactory.GetFont(Font, FontStyle.Bold))
            {
                foreach (Cell cell in m_rootCell.AllCells)
                {
                    DrawCell(g, cell, boldFont, ofsLeft, ofsTop);
                }
            }
        }

        /// <summary>
        /// Draws the line between the given cells and recursively repeats the operation for the children cell.
        /// </summary>
        /// <param name="g"></param>
        /// <param name="startCell"></param>
        /// <param name="endCell"></param>
        /// <param name="pen"></param>
        /// <param name="ofsLeft"></param>
        /// <param name="ofsTop"></param>
        private void DrawLines(Graphics g, Cell startCell, Cell endCell, Pen pen, int ofsLeft, int ofsTop)
        {
            Rectangle startRect = startCell.m_rectangle;
            Rectangle endRect = endCell.m_rectangle;

            g.DrawLine(pen,
                       startRect.Location.X + ofsLeft + (startRect.Width >> 1),
                       startRect.Location.Y + ofsTop + (startRect.Height >> 1),
                       endRect.Location.X + ofsLeft + (startRect.Width >> 1),
                       endRect.Location.Y + ofsTop + (startRect.Height >> 1));

            foreach (Cell child in endCell.m_cells)
            {
                DrawLines(g, endCell, child, pen, ofsLeft, ofsTop);
            }
        }

        /// <summary>
        /// Paints the provided cell and recursively repeats the operation for the children cell.
        /// </summary>
        /// <param name="g"></param>
        /// <param name="cell"></param>
        /// <param name="boldFont"></param>
        /// <param name="ofsLeft"></param>
        /// <param name="ofsTop"></param>
        private void DrawCell(Graphics g, Cell cell, Font boldFont, int ofsLeft, int ofsTop)
        {
            Rectangle rect = cell.m_rectangle;
            rect.Offset(ofsLeft, ofsTop);

            Color stdTextColor = !Settings.UI.SafeForWork ? Color.Black : SystemColors.ControlText;
            Color reqTextColor = !Settings.UI.SafeForWork ? Color.Red : SystemColors.GrayText;
            Color prTextColor = !Settings.UI.SafeForWork ? Color.Yellow : SystemColors.ControlText;

            Brush fillBrush = null;
            try
            {
                StringBuilder currentLevelText = new StringBuilder();

                // Retrieves the output of the second line : "Current Level : II (Planned to IV)"
                currentLevelText.AppendFormat(CultureConstants.DefaultCulture,
                                              "Current Level: {0}",
                                              Skill.GetRomanFromInt(cell.m_skill.Level));

                if (m_plan.GetPlannedLevel(cell.m_skill) > 0)
                {
                    currentLevelText.AppendFormat(CultureConstants.DefaultCulture,
                                                  " (Planned To: {0})",
                                                  Skill.GetRomanFromInt(m_plan.GetPlannedLevel(cell.m_skill)));
                }

                // Retrieves the output and colors for the lower lines
                string thisRequiredTime = null;
                string requiredLevel = null;
                string prereqTime = null;
                if (cell.m_requiredLevel > 0)
                {
                    // Third line : "Required Level : V"
                    requiredLevel = String.Format(CultureConstants.DefaultCulture, "Required Level: {0}",
                                                  Skill.GetRomanFromInt(cell.m_requiredLevel));

                    if (cell.m_requiredLevel > cell.m_skill.Level)
                    {
                        // Fourth line : "This Time : 9H, 26M, 42S"
                        TimeSpan ts = cell.m_skill.GetLeftTrainingTimeToLevel(cell.m_requiredLevel);
                        thisRequiredTime = String.Format(CultureConstants.DefaultCulture, "This Time: {0}",
                                                         ts.ToDescriptiveText(TimeFormat));
                        reqTextColor = !Settings.UI.SafeForWork ? Color.Yellow : SystemColors.GrayText;

                        if (cell.m_skill.ArePrerequisitesMet)
                        {
                            fillBrush = new LinearGradientBrush(rect, Color.LightPink, Color.DarkRed, 90.0F);
                        }
                        else
                        {
                            fillBrush = new LinearGradientBrush(rect, Color.Red, Color.Black, 90.0F);
                            stdTextColor = !Settings.UI.SafeForWork ? Color.White : SystemColors.ControlText;
                        }
                    }
                        // Required level already met
                    else
                    {
                        reqTextColor = !Settings.UI.SafeForWork ? Color.Black : SystemColors.ControlText;
                        fillBrush = new LinearGradientBrush(rect, Color.LightSeaGreen, Color.DarkGreen, 90.0F);
                    }
                }
                    // Skill at level 0, prerequisites met
                else if (cell.m_skill.ArePrerequisitesMet)
                {
                    fillBrush = new LinearGradientBrush(rect, Color.LightBlue, Color.Blue, 90.0F);
                }
                    // Skill unknown, not trainable
                else
                {
                    fillBrush = new LinearGradientBrush(rect, Color.Blue, Color.Black, 90.0F);
                    stdTextColor = !Settings.UI.SafeForWork ? Color.White : SystemColors.ControlText;
                }

                // Last line : prerequisites time
                if (!cell.m_skill.ArePrerequisitesMet)
                {
                    TimeSpan pts = cell.m_skill.Character.GetTrainingTimeToMultipleSkills(cell.m_skill.Prerequisites);
                    prereqTime = String.Format(CultureConstants.DefaultCulture, "Prerequisite: {0}",
                                               pts.ToDescriptiveText(TimeFormat));
                }



                // Fill the background
                if (Settings.UI.SafeForWork)
                {
                    fillBrush.Dispose();
                    fillBrush = new SolidBrush(SystemColors.Control);
                }
                g.FillRectangle(fillBrush, rect);


                // Draw text (two to five lines)
                Point drawPoint = new Point(rect.Left + 5, rect.Top + 5);
                Size sz = MeasureAndDrawText(g, cell.m_skill.Name, boldFont, drawPoint, stdTextColor);
                drawPoint.Y += sz.Height;

                sz = MeasureAndDrawText(g, currentLevelText.ToString(), Font, drawPoint, stdTextColor);
                drawPoint.Y += sz.Height;

                if (!String.IsNullOrEmpty(requiredLevel))
                {
                    sz = MeasureAndDrawText(g, requiredLevel, Font, drawPoint, reqTextColor);
                    drawPoint.Y += sz.Height;
                }
                if (!String.IsNullOrEmpty(thisRequiredTime))
                {
                    sz = MeasureAndDrawText(g, thisRequiredTime, Font, drawPoint, reqTextColor);
                    drawPoint.Y += sz.Height;
                }
                if (!String.IsNullOrEmpty(prereqTime))
                {
                    sz = MeasureAndDrawText(g, prereqTime, Font, drawPoint, prTextColor);
                    drawPoint.Y += sz.Height;
                }

                // Draw border
                g.DrawRectangle(!Settings.UI.SafeForWork ? Pens.Black : SystemPens.ControlDarkDark, rect);
            }
            finally
            {
                if (fillBrush != null)
                    fillBrush.Dispose();
            }
        }

        /// <summary>
        /// Simultaneously measures and renders the text.
        /// </summary>
        /// <param name="g"></param>
        /// <param name="text"></param>
        /// <param name="f"></param>
        /// <param name="p"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        private Size MeasureAndDrawText(IDeviceContext g, string text, Font f, Point p, Color c)
        {
            Size res = TextRenderer.MeasureText(g, text, f);

            TextRenderer.DrawText(g, text, f,
                                  new Rectangle(p.X, p.Y, res.Width, res.Height), c, Color.Transparent,
                                  TextFormatFlags.Default);
            return res;
        }

        /// <summary>
        /// Once the size changed, we rearrange the graph and update scroll bars.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSizeChanged(EventArgs e)
        {
            ArrangeGraph();
            base.OnSizeChanged(e);
        }

        #endregion


        #region Other events

        /// <summary>
        /// When the root skill or one of its prerequisites is in training,
        /// every 30s we invalidate the display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrSkillTick_Tick(object sender, EventArgs e)
        {
            Invalidate();
            CheckTraining();
        }

        /// <summary>
        /// On click, we detect which skill is under the mouse location.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            // Computes the offsets caused by scrollers
            int ofsLeft = -AutoScrollPosition.X;
            int ofsTop = -AutoScrollPosition.Y;

            // Checks every cell
            Skill skill = null;
            Point mouseLocation = e.Location;
            mouseLocation.Offset(ofsLeft, ofsTop);
            foreach (Cell cell in m_rootCell.AllCells.Where(x => x.m_rectangle.Contains(mouseLocation)))
            {
                skill = cell.m_skill;
            }

            // Fires the event when skill not null
            if (skill == null || SkillClicked == null)
                return;

            SkillClicked(this, new SkillClickedEventArgs(skill, e.Button, mouseLocation));
        }

        /// <summary>
        /// On scrolling, we invalidate the display.
        /// </summary>
        /// <param name="se"></param>
        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }

        /// <summary>
        /// On mouse wheel, the scroll changes, so we invalidate.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate();
        }

        /// <summary>
        /// On settings change, we invalidate the drawing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EveMonClient_SettingsChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        /// <summary>
        /// Fired when one of the character changed (skill completion, update from CCP, etc).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EveMonClient_CharacterUpdated(object sender, CharacterChangedEventArgs e)
        {
            if (m_plan == null)
                return;

            if (e.Character != m_plan.Character)
                return;

            Invalidate();
        }

        /// <summary>
        /// Occurs when the plan changes, we invalidate the drawing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EveMonClient_PlanChanged(object sender, PlanChangedEventArgs e)
        {
            Invalidate();
        }

        #endregion


        #region Private Support Classes

        #region Private Class "Row"

        /// <summary>
        /// Represents a cells' row.
        /// </summary>
        private sealed class Row : List<Cell>
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public Row(Cell cell)
            {
                Add(cell);
            }

            /// <summary>
            /// Gets true if two cells overlaps or if their space is lesser than the regular spacing.
            /// </summary>
            /// <param name="leftIndex"></param>
            /// <param name="rightIndex"></param>
            /// <param name="space">The space between the two cells, including the margins. Negative when boxes are overlapping.</param>
            /// <returns></returns>
            public bool AreOverlapping(int leftIndex, int rightIndex, out int space)
            {
                space = 0;
                if (leftIndex == rightIndex || rightIndex >= Count || leftIndex >= Count || rightIndex < -1 ||
                    leftIndex < -1)
                    return false;

                Cell left = this[Math.Min(leftIndex, rightIndex)];
                Cell right = this[Math.Max(leftIndex, rightIndex)];

                space = right.m_rectangle.Left - (left.m_rectangle.Right + SkillboxMarginLr);
                return (space < 0);
            }
        }

        #endregion


        #region Private Class "Cell"

        /// <summary>
        /// Helper class to store layout informations about a skill
        /// </summary>
        private sealed class Cell
        {
            public readonly Skill m_skill;
            public readonly List<Cell> m_cells = new List<Cell>();
            public readonly int m_requiredLevel = -1;

            private readonly SkillTreeDisplayControl m_std = new SkillTreeDisplayControl();

            public Rectangle m_rectangle = Rectangle.Empty;

            /// <summary>
            /// Constructor for root.
            /// </summary>
            /// <param name="skill"></param>
            public Cell(Skill skill)
            {
                m_skill = skill;

                // Create the top row
                List<Row> rows = new List<Row> {new Row(this)};

                // Create the children
                foreach (SkillLevel prereq in skill.Prerequisites.Where(prereq => prereq.Skill != skill))
                {
                    m_cells.Add(new Cell(prereq, rows, 1));
                }

                // Perform the layout
                FirstPassLayout(0, 0);
                SecondPassLayout(rows, 0);
            }

            /// <summary>
            /// Constructor for prerequisites.
            /// </summary>
            /// <param name="rows"></param>
            /// <param name="level"></param>
            /// <param name="prereq"></param>
            private Cell(SkillLevel prereq, IList<Row> rows, int level)
            {
                m_skill = prereq.Skill;
                m_requiredLevel = prereq.Level;

                // Put on the appropriate row
                if (rows.Count == level)
                {
                    rows.Add(new Row(this));
                }
                else
                {
                    rows[level].Add(this);
                }

                // Create the children
                foreach (
                    SkillLevel childPrereq in
                        prereq.Skill.Prerequisites.Where(childPrereq => childPrereq.Skill != prereq.Skill))
                {
                    m_cells.Add(new Cell(childPrereq, rows, level + 1));
                }
            }

            /// <summary>
            /// Arrange cells in a hierarchical order matching prerequisites,
            /// the first one being centered on x = 0.
            /// </summary>
            /// <param name="left"></param>
            /// <param name="top"></param>
            /// <returns></returns>
            private void FirstPassLayout(int left, int top)
            {
                // Layout this cell
                m_rectangle = new Rectangle(left - m_std.CellWidth/2, top, m_std.CellWidth, m_std.CellHeight);

                // Layout the children
                int childrenTop = top + m_std.CellHeight + SkillboxMarginUd;
                int childrenWidth = m_cells.Count*m_std.CellWidth + (m_cells.Count - 1)*SkillboxMarginLr;

                left += (m_std.CellWidth - childrenWidth)/2;
                foreach (Cell cell in m_cells)
                {
                    cell.FirstPassLayout(left, childrenTop);
                    left += m_std.CellWidth + SkillboxMarginLr;
                }
            }

            /// <summary>
            /// The first pass may have created overlapping rectangles,
            /// so we check every row and shift boxes when required.
            /// </summary>
            /// <param name="rows"></param>
            /// <param name="level"></param>
            private void SecondPassLayout(IList<Row> rows, int level)
            {
                // Gets the row for this level
                if (level == rows.Count)
                    return;

                Row row = rows[level];

                // Scan every cell and, when there is a conflict, shift all the other cells
                for (int i = 0; i < row.Count - 1; i++)
                {
                    int space;
                    if (!row.AreOverlapping(i, i + 1, out space))
                        continue;

                    // Shift the two cells
                    int shift = (-space) >> 1;
                    row[i].Offset(-shift, 0);
                    row[i + 1].Offset(shift, 0);

                    // Shift boxes on the left of "left"
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (!row.AreOverlapping(j, j + 1, out space))
                            break;

                        row[j].Offset(space, 0);
                    }
                    // Shift boxes on the right of "right"
                    for (int j = i + 2; j < row.Count; j++)
                    {
                        if (!row.AreOverlapping(j, j - 1, out space))
                            break;

                        row[j].Offset(-space, 0);
                    }
                }

                // Next level
                SecondPassLayout(rows, level + 1);
            }

            /// <summary>
            /// Offsets this cell and all its children.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            private void Offset(int x, int y)
            {
                m_rectangle.Offset(x, y);
                foreach (Cell cell in m_cells)
                {
                    cell.Offset(x, y);
                }
            }

            /// <summary>
            /// Arranges the whole graph into the given surface. Centers the cells and then return the global bounding box.
            /// </summary>
            /// <param name="size"></param>
            /// <returns>The graph bounds, centered into the given surface (or aligned on top left when there is overlap.</returns>
            public Rectangle Arrange(Size size)
            {
                // Compute the global rect
                int left = m_rectangle.Left,
                    right = m_rectangle.Right,
                    top = m_rectangle.Top,
                    bottom = m_rectangle.Bottom;
                foreach (Rectangle rect in AllCells.Select(cell => cell.m_rectangle))
                {
                    left = Math.Min(left, rect.Left);
                    right = Math.Max(right, rect.Right);
                    bottom = Math.Max(bottom, rect.Bottom);
                    top = Math.Min(top, rect.Top);
                }

                // Compute the new origins
                int xOrigin = Math.Max(10, size.Width - (right - left)) >> 1;
                int yOrigin = Math.Max(10, size.Height - (bottom - top)) >> 1;

                // Offset the cell's rectangles
                foreach (Cell cell in AllCells)
                {
                    cell.m_rectangle.Offset(xOrigin - left, yOrigin - top);
                }

                // Return the global rect
                return new Rectangle(xOrigin, yOrigin, 20 + right - left, 20 + bottom - top);
            }

            /// <summary>
            /// Gets this cell and all its descendants.
            /// </summary>
            public IEnumerable<Cell> AllCells
            {
                get
                {
                    yield return this;
                    foreach (Cell cell in m_cells.SelectMany(child => child.AllCells))
                    {
                        yield return cell;
                    }
                }
            }
        }

        #endregion

        #endregion
    }
}