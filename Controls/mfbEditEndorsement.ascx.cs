﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using AjaxControlToolkit;
using System.Text.RegularExpressions;
using MyFlightbook;
using MyFlightbook.Instruction;

/******************************************************
 * 
 * Copyright (c) 2015 MyFlightbook LLC
 * Contact myflightbook-at-gmail.com for more information
 *
*******************************************************/

public partial class Controls_mfbEditEndorsement : System.Web.UI.UserControl
{
    private const string ctlIDPrefix = "endrsTemplCtl";
    private const string keyVSTargetUser = "vsTarget";
    private const string keyVSSourceUser = "vsSource";
    private const string keyVSStudentType = "vsStudentType";

    public event EventHandler<EndorsementEventArgs> NewEndorsement = null;

    private int m_endorsementID = 0;

    private MyFlightbook.Profile m_TargetUser = null;
    private MyFlightbook.Profile m_SourceUser = null;

    /// <summary>
    /// Student
    /// </summary>
    public MyFlightbook.Profile TargetUser
    {
        get { return m_TargetUser; }
        set 
        {
            if (value == null)
                throw new ArgumentNullException("value");
            m_TargetUser = value; 
            lblStudent.Text = value.UserFullName;
            ViewState[keyVSTargetUser] = value.UserName;
        }
    }

    /// <summary>
    /// Type of student endorsement - member or external
    /// </summary>
    public Endorsement.StudentTypes StudentType 
    {
        get
        {
            if (ViewState[keyVSStudentType] == null)
                ViewState[keyVSStudentType] = (int)Endorsement.StudentTypes.Member;
            return (Endorsement.StudentTypes)ViewState[keyVSStudentType];
        }
        set 
        {
            ViewState[keyVSStudentType] = (int)value;
            mvStudent.SetActiveView(value == Endorsement.StudentTypes.Member ? vwStudentAuthenticated : vwStudentOffline);
        }
    }

    /// <summary>
    /// CFI
    /// </summary>
    public MyFlightbook.Profile SourceUser
    {
        get { return m_SourceUser; }
        set
        {
            if (value == null)
                throw new ArgumentNullException("value");
            m_SourceUser = value;
            lblCFI.Text = value.UserFullName;
            lblCFICert.Text = value.Certificate;
            lblCFIExp.Text = value.CertificateExpiration.ToShortDateString();
            ViewState[keyVSSourceUser] = value.UserName;
        }
    }

    /// <summary>
    /// Just previewing?
    /// </summary>
    public bool PreviewMode
    {
        get { return !btnAddEndorsement.Visible; }
        set { btnAddEndorsement.Visible = !value; }
    }

    /// <summary>
    /// ID of the endorsment to use.  Forces a redraw.
    /// </summary>
    public int EndorsementID 
    { 
        get {return m_endorsementID;}
        set { m_endorsementID = value; UpdateFormForTemplate(m_endorsementID); }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            mfbTypeInDate1.Date = mfbTypeInDate1.DefaultDate = DateTime.Now;
        }
        else
        {
            SourceUser = MyFlightbook.Profile.GetUser((string)ViewState[keyVSSourceUser] ?? Page.User.Identity.Name);
            string szTarget = (string) ViewState[keyVSTargetUser];
            if (!String.IsNullOrEmpty(szTarget))
                TargetUser = MyFlightbook.Profile.GetUser(szTarget);
        }
    }

    #region hack subclasses
    /// <summary>
    /// Hack: We get a CA2000 ("Dispose objects before losing scope") code analysis error if we do ANYTHING (such as setting an ID) to a control between creation of the object and AddControl.
    /// But we can't set the ID as part of the creation processes for TextBoxWatermarkExtender, which means we can crash on adding it due to duplicate control ID's (since the default
    /// TextBoxWatermarkExtender has a null ID, and null is already used for the placeholder).  SO...instead we'll create a subclass that takes the ID (and what the heck, other paramters too)
    /// in the constructor, and we can add that directly.
    /// </summary>
    protected class EndorsementWatermark : TextBoxWatermarkExtender
    {
        public EndorsementWatermark(string id, string target, string wmText)
            : base()
        {
            ID = id;
            TargetControlID = target;
            WatermarkText = wmText;

            WatermarkCssClass = "watermark";
        }
    }

    protected class EndorsementTextBox : TextBox
    {
        public EndorsementTextBox(string id, string txt) : base()
        {
            ID = id;
            Text = txt;

            BorderStyle = BorderStyle.Solid;
            BorderWidth = Unit.Pixel(1);
            BorderColor = System.Drawing.Color.Black;
        }
    }

    protected class EndorsementRequiredField : RequiredFieldValidator
    {
        public EndorsementRequiredField(string id, string targetID, string msg) : base()
        {
            ID = id;
            ControlToValidate = targetID;
            ErrorMessage = msg;

            CssClass = "error";
            Display = ValidatorDisplay.Dynamic;
        }
    }
    #endregion

    protected void NewTextBox(Control parent, string id, string szDefault, Boolean fMultiline, Boolean fRequired, string szName)
    {
        if (parent == null)
            throw new ArgumentNullException("parent");
        TextBox tb = new EndorsementTextBox(id, szDefault);
        parent.Controls.Add(tb);

        if (fMultiline)
        {
            tb.Rows = 4;
            tb.Width = Unit.Percentage(100);
            tb.TextMode = TextBoxMode.MultiLine;
        }
        else
            parent.Controls.Add(new EndorsementWatermark("wm" + id, tb.ID, szName)); // Watermark is for single-line only.

        // no validations for preview mode
        if (fRequired && !PreviewMode)
            plcValidations.Controls.Add(new EndorsementRequiredField("val" + id, tb.ID, String.Format(CultureInfo.CurrentCulture, Resources.SignOff.EditEndorsementRequiredField, szName)));
    }

    protected void UpdateFormForTemplate(int id)
    {
        EndorsementType et = EndorsementType.GetEndorsementByID(id);
        if (et == null)
            throw new MyFlightbookException(String.Format(CultureInfo.InvariantCulture, "EndorsementTemplate with ID={0} not found", id));

        plcTemplateForm.Controls.Clear();
        plcValidations.Controls.Clear();

        lblEndorsementTitle.Text = et.Title;
        lblEndorsementFAR.Text = et.FARReference;

        // Find each of the substitutions
        Regex r = new Regex("\\{[^}]*\\}");
        MatchCollection matches = r.Matches(et.BodyTemplate);

        int cursor = 0;
        foreach (Match m in matches)
        {
            // compute the base ID for a control that we create here, before anything gets added, since the result depends on how many controls are in the placeholder already
            string idNewControl = ctlIDPrefix + plcTemplateForm.Controls.Count.ToString(CultureInfo.InvariantCulture); 

            if (m.Index > cursor) // need to catch up on some literal text
            {
                LiteralControl lt = new LiteralControl();
                plcTemplateForm.Controls.Add(lt);
                lt.Text = et.BodyTemplate.Substring(cursor, m.Index - cursor);
                lt.ID = "ltCatchup" + idNewControl;
            }

            string szMatch = m.Captures[0].Value;

            switch (szMatch)
            {
                case "{Date}":
                    {
                        Controls_mfbTypeInDate tid = (Controls_mfbTypeInDate)LoadControl("~/Controls/mfbTypeInDate.ascx");
                        plcTemplateForm.Controls.Add(tid);
                        tid.Date = DateTime.Now;
                        tid.ID = idNewControl;
                        tid.TextControl.BorderColor = System.Drawing.Color.Black;
                        tid.TextControl.BorderStyle = BorderStyle.Solid;
                        tid.TextControl.BorderWidth = Unit.Pixel(1);
                    }
                    break;
                case "{FreeForm}":
                    NewTextBox(plcTemplateForm, idNewControl, "", true, true, "Free-form text");
                    break;
                case "{Student}":
                    NewTextBox(plcTemplateForm, idNewControl, TargetUser == null ? Resources.SignOff.EditEndorsementStudentNamePrompt : TargetUser.UserFullName, false, true, Resources.SignOff.EditEndorsementStudentNamePrompt);
                    break;
                default:
                    // straight textbox, unless it is strings separated by slashes, in which case it's a drop-down
                    {
                        string szMatchInner = szMatch.Substring(1, szMatch.Length - 2);  // get the inside bits - i.e., strip off the curly braces
                        if (szMatchInner.Contains("/"))
                        {
                            string[] rgItems = szMatchInner.Split('/');
                            DropDownList dl = new DropDownList();
                            plcTemplateForm.Controls.Add(dl);
                            foreach (string szItem in rgItems)
                                dl.Items.Add(new ListItem(szItem, szItem));
                            dl.ID = idNewControl;
                            dl.BorderColor = System.Drawing.Color.Black;
                            dl.BorderStyle = BorderStyle.Solid;
                            dl.BorderWidth = Unit.Pixel(1);
                        }
                        else
                            NewTextBox(plcTemplateForm, idNewControl, String.Empty, false, true, szMatchInner);
                    }
                    break;
            }

            cursor = m.Captures[0].Index + m.Captures[0].Length;
        }

        if (cursor < et.BodyTemplate.Length)
        {
            LiteralControl lt = new LiteralControl();
            plcTemplateForm.Controls.Add(lt);
            lt.Text = et.BodyTemplate.Substring(cursor);
            lt.ID = "ltEnding";
        }
    }

    protected string TemplateText()
    {
        StringBuilder sb = new StringBuilder();

        foreach (Control c in plcTemplateForm.Controls)
        {
            LiteralControl l = c as LiteralControl;
            TextBox t = c as TextBox;
            Controls_mfbTypeInDate d = c as Controls_mfbTypeInDate;
            DropDownList dd = c as DropDownList;

            if (l != null)
                sb.Append(l.Text);
            else if (t != null)
                sb.Append(t.Text);
            else if (d != null)
                sb.Append(d.Date.ToShortDateString());
            else if (dd != null)
                sb.Append(dd.SelectedValue);
        }

        return sb.ToString();
    }

    protected void btnAddEndorsement_Click(object sender, EventArgs e)
    {
        if (Page.IsValid && NewEndorsement != null)
        {
            Endorsement endorsement = new Endorsement(Page.User.Identity.Name);
            Profile pf = MyFlightbook.Profile.GetUser(Page.User.Identity.Name);
            endorsement.CFICertificate = pf.Certificate;
            endorsement.CFIExpirationDate = pf.CertificateExpiration;
            endorsement.Date = mfbTypeInDate1.Date;
            endorsement.EndorsementText = TemplateText();
            endorsement.StudentType = StudentType;
            endorsement.StudentName = StudentType == Endorsement.StudentTypes.Member ? TargetUser.UserName : txtOfflineStudent.Text;
            endorsement.Title = lblEndorsementTitle.Text;
            endorsement.FARReference = lblEndorsementFAR.Text;

            NewEndorsement(this, new EndorsementEventArgs(endorsement));
        }
    }
    protected void valNoBackDate_ServerValidate(object source, ServerValidateEventArgs args)
    {
        if (args == null)
            throw new ArgumentNullException("args");
        // Offer up to 20 days to get the endorsement in
        if (StudentType == Endorsement.StudentTypes.Member && DateTime.Now.AddDays(-20).CompareTo(mfbTypeInDate1.Date) > 0)
            args.IsValid = false;
    }

    protected void valNoPostDate_ServerValidate(object source, ServerValidateEventArgs args)
    {
        if (args == null)
            throw new ArgumentNullException("args");
        // Don't allow any post dating, but allow 1 day of slop due to time zone
        if (DateTime.Now.AddDays(1).CompareTo(mfbTypeInDate1.Date) < 0)
            args.IsValid = false;
    }
}