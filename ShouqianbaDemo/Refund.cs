//
//  Refund.cs
//  ShouqianbaDemo
//
//  Created by Wosai on 16-5-11.
//  Copyright (c) 2016年 Wosai. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ShouqianbaDemo
{
    public partial class Refund : Form
    {
        public Refund()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string num = money_text.Text;

            if (String.IsNullOrEmpty(num))
            {
                MessageBox.Show("请输入退款金额");
                return;
            }

            Main.refund_amount = num;

            
            this.DialogResult = DialogResult.OK;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
