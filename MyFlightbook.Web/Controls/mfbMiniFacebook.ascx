﻿<%@ Control Language="C#" AutoEventWireup="true" Codebehind="mfbMiniFacebook.ascx.cs" Inherits="Controls_mfbMiniFacebook" %>
<asp:HyperLink ID="lnkFBAdd" runat="server" Target="_blank">
    <asp:Image ID="imgFBButton" ImageUrl="~/images/facebookicon.gif" runat="server" style="padding-right:8px;"
        Tooltip="<%$ Resources:LocalizedText, MiniFacebookAddToFacebook %>" AlternateText="<%$ Resources:LocalizedText, MiniFacebookAddToFacebook %>" ></asp:Image>
    <asp:Label ID="lblPostFB" runat="server" Text="<%$ Resources:LocalizedText, MiniFacebookAddToFacebook %>"></asp:Label>
</asp:HyperLink>