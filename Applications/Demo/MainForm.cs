﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using Codeology.SharpCache;
using Codeology.SharpCache.Providers;

namespace Demo
{

    public partial class MainForm : Form
    {

        public MainForm()
        {
            InitializeComponent();

            // Create provider
            ICacheProvider provider = new MemcacheCacheProvider("Test","127.0.0.1:11211");

            provider.Initialize();

            // Register provider
            Cache.RegisterProvider(provider,true);
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            Cache.SetAsync(txtKey.Text,txtValue.Text,1);

            txtKey.Clear();
            txtValue.Clear();
        }

        private void btnGet_Click(object sender, EventArgs e)
        {
            if (!Cache.Exists(txtKey.Text)) {
                txtOutput.Text += "Item does not exist." + Environment.NewLine;
            } else {
                object value = Cache.Get(txtKey.Text);

                if (value != null) {
                    txtOutput.Text += value.ToString() + Environment.NewLine;
                } else {
                    txtOutput.Text += "Null" + Environment.NewLine;
                }
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            Cache.Clear();

            txtKey.Clear();
            txtValue.Clear();
            txtOutput.Clear();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Cache.DefaultProvider.Uninitialize();
        }

    }

}
