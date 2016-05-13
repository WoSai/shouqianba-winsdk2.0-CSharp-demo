//
//  Main.cs
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
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.IO;
using System.Threading;

namespace ShouqianbaDemo
{
    public partial class Main : Form
    {
        #region CashBar SDK API

        [DllImport("kernel32.dll")]
        private extern static IntPtr LoadLibrary(String path);
        [DllImport("kernel32.dll")]
        private extern static IntPtr GetProcAddress(IntPtr lib, String funcName);
        [DllImport("kernel32.dll")]
        private extern static bool FreeLibrary(IntPtr lib);
        private IntPtr hLib;

        public delegate int int_int(int need);
        public delegate int int_str(string cmd);
        public unsafe delegate byte* str_str(string cmd); //如出现错误提示，在工程属性“生成”中，将“允许不安全代码”勾上//
        public unsafe delegate byte* str_void();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        //将要执行的函数转换为委托//
        public Delegate Invoke(String APIName, Type t)
        {
            IntPtr api = GetProcAddress(hLib, APIName);
            return (Delegate)Marshal.GetDelegateForFunctionPointer(api, t);
        }
        #endregion



        private string terminal_sn = "";
        private Thread pay_thread = null;
        private Thread refund_thread = null;
        private Thread revoke_thread = null;
        private string amount = "";
        public static string refund_amount = "";
        unsafe public Main()
        {
            InitializeComponent();

            
            hLib = LoadLibrary("./../../CashBarV2.dll"); //加载sdk，注意路径，如加载不成功，请在工程配置，“生成”中把“目标CPU”改成x86//
            //参考此目录下的keyparams文件控制测试环境和正式环境，RC为测试环境，RTM为正式环境//
        }

        private void Main_Load(object sender, EventArgs e)
        {
            pay_way_box.SelectedIndex = 0;

            byte[] buffer = Guid.NewGuid().ToByteArray();
            string order_sn = BitConverter.ToInt64(buffer, 0).ToString();
            own_order_text.Text = order_sn;
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            FreeLibrary(hLib); //卸载sdk//
        }

        /// <summary>
        /// 点击激活按钮。激活前需要先判断是否已经激活，激活只需进行一次，不要重复激活。
        /// 注意激活码对应的是测试环境还是正式环境
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        unsafe private void activate_sdk_btn_Click(object sender, EventArgs e)
        {
            string vendor_sn = vendorsn_text.Text;
            string vendor_key = vendorkey_text.Text;
            string activate_code = activate_code_text.Text;

            if (vendor_sn.Equals(""))
            {
                MessageBox.Show("请输入vendor sn");
                return;
            }
            if (vendor_key.Equals(""))
            {
                MessageBox.Show("请输入vendor key");
                return;
            }
            if (activate_code.Equals(""))
            {
                MessageBox.Show("请输入激活码");
                return;
            }

            //先判断是否已经激活,如果需要重新激活，请删除exe目录下的AutoKeyParams文件//
            if (isActivated())
            {
                MessageBox.Show("设备已经激活，请勿重复激活");
                return;
            }

            //激活流程//
            string code = vendor_sn + "&" + vendor_key + "&" + activate_code;

            insertRecord("开始激活：" + code);

            //开启自动转码//
            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            //调用sdk activate方法//
            str_str activate = (str_str)Invoke("_activate@4", typeof(str_str));
            byte* ret_p = activate(code);

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            //获取激活结果//
            string ret = System.Text.Encoding.Default.GetString(ret_test);
            insertRecord(ret);
            if (!ret.Contains("Activate Success."))
            {
                MessageBox.Show("激活失败，请重试");
            }
            else
            {
                MessageBox.Show("激活成功，可以开始交易了");
            }
        }

        /// <summary>
        /// 返回是否已激活
        /// </summary>
        /// <returns></returns>
        unsafe bool isActivated()
        {
            bool is_activate = false;
            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            str_void ternimal = (str_void)Invoke("_terminalSN@0", typeof(str_void));
            byte* ret_p = ternimal();

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);
            ret = ret.Replace("\0", "");
            if (ret != "")
            {
                is_activate = true;
                terminal_sn = ret; //这是终端号//
            }
            else
            {
                is_activate = false;
            }
            return is_activate;
        }


        /// <summary>
        /// 点击收款按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cash_btn_Click(object sender, EventArgs e)
        {
            amount = total_amount_text.Text;
            string order_sn = own_order_text.Text;
            string goods_name = goods_name_text.Text;
            string goods_des = goods_des_text.Text;
            string operator_id = operator_text.Text;
            string pay_code = pay_code_text.Text;
            string reflect = reflect_text.Text;
            string externd = extern_text.Text;
            int select_way = pay_way_box.SelectedIndex;
            string pay_way = select_way.ToString();

            //未指定支付方式时，传空//
            if (select_way == 0)
                pay_way = "";

            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            string cmd = order_sn + "&" + goods_name + "&" + operator_id + "&" +
                goods_des + "&" + pay_way + "&" + amount + "&" + pay_code + "&" + reflect + "&" + externd;


            insertRecord("开始收款:"+cmd);

            //使用异步收款//
            if (pay_thread != null)
            {
                pay_thread = null;
            }

            if (pay_thread == null)
            {
                cash_btn.Enabled = false;
                pay_thread = new Thread(delegate() { pay_method(cmd); });
                pay_thread.Name = "pay thread";
                pay_thread.Start();
            }
        }


        unsafe private void pay_method(string cmd)
        {
            //开启自动转码//
            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            //调用sdk pay方法//
            str_str pay;
            if (ui_checkbox.Checked)//是否使用UI界面//
            {
                pay = (str_str)Invoke("_payUI@4", typeof(str_str));
            }
            else
            {
                pay = (str_str)Invoke("_pay@4", typeof(str_str));
            }
               
            byte* ret_p = pay(cmd);

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);
            ret = ret.Replace("\0", "");

            //设置结果回调//
            PayCallBackDelegate mi = new PayCallBackDelegate(payCallBack);
            this.BeginInvoke(mi, new object[] { ret });
        }

        public delegate void PayCallBackDelegate(string ret);

        /// <summary>
        /// 返回参考sdk文档
        /// </summary>
        /// <param name="ret"></param>
        public void payCallBack(string ret)
        {
            insertRecord(ret);
            pay_thread.Abort();
            cash_btn.Enabled = true;
            string[] split = ret.Split(new char[] { '&' });
            if (ret.Contains("Pay Success."))
            {
                ret = ret.Replace("Pay Success.", "支付成功，");
                
                string order_sn = split[0].Substring(12);

                wosai_order_text.Text = order_sn;
                wosai_order_text.ForeColor = Color.Black;
            }
        }



        private void insertRecord(string log)
        {
            System.DateTime currentTime = new System.DateTime();
            currentTime = System.DateTime.Now;

            log = currentTime + ":" +log;

            if (String.IsNullOrEmpty(log_view.Text))
            {
                log_view.Text = log;
            }
            else
            {
                log_view.Text += "\r\n\r\n" + log;
            }
        }

        private void clear_log_btn_Click(object sender, EventArgs e)
        {
            log_view.Text = "";
        }


        public void RemoveText(object sender, EventArgs e)
        {
            if (wosai_order_text.ForeColor == Color.DarkGray)
            {
                wosai_order_text.Text = "";
                wosai_order_text.ForeColor = Color.Black;
            }
        }

        public void AddText(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(wosai_order_text.Text))
            {
                wosai_order_text.Text = "收款成功后自动输入";
                wosai_order_text.ForeColor = Color.DarkGray;
            }
        }

        /// <summary>
        /// 用收钱吧订单号退款，参数参考sdk文档
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wosai_refund_btn_Click(object sender, EventArgs e)
        {
            Refund refund_form = new Refund();
            if (refund_form.ShowDialog() == DialogResult.OK)
            {
                string cmd = wosai_order_text.Text + "&&" + refund_text.Text + "&" + operator_text.Text + "&" + refund_amount + "&" + reflect_text.Text;

                insertRecord("开始退款："+cmd);

                if (refund_thread != null)
                {
                    refund_thread = null;
                }

                if (refund_thread == null)
                {
                    wosai_refund_btn.Enabled = false;
                    refund_thread = new Thread(delegate() { refund_method(cmd); });
                    refund_thread.Name = "refund thread";
                    refund_thread.Start();
                }

            }
        }

        /// <summary>
        /// 用商户订单号退款，参数参考sdk文档
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void own_refund_btn_Click(object sender, EventArgs e)
        {
            Refund refund_form = new Refund();
            if (refund_form.ShowDialog() == DialogResult.OK)
            {
                string cmd = "&" + own_order_text.Text + "&" + refund_text.Text + "&" + operator_text.Text + "&" + refund_amount + "&" + reflect_text.Text;

                insertRecord("开始退款：" + cmd);

                if (refund_thread != null)
                {
                    refund_thread = null;
                }

                if (refund_thread == null)
                {
                    wosai_refund_btn.Enabled = false;
                    refund_thread = new Thread(delegate() { refund_method(cmd); });
                    refund_thread.Name = "refund thread";
                    refund_thread.Start();
                }

            }
        }

        /// <summary>
        /// 退款方法
        /// </summary>
        /// <param name="cmd"></param>
        unsafe private void refund_method(string cmd)
        {
            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            str_str refund;
            if(ui_checkbox.Checked) //是否使用UI界面//
            {
                refund = (str_str)Invoke("_refundUI@4", typeof(str_str));
            }
            else
            {
                refund = (str_str)Invoke("_refund@4", typeof(str_str));
            }

            byte* ret_p = refund(cmd);

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);

            RefundCallBackDelegate mi = new RefundCallBackDelegate(refundCallBack);
            this.BeginInvoke(mi, new object[] { ret });
        }

        public delegate void RefundCallBackDelegate(string ret);

        /// <summary>
        /// 退款回调，参数参考sdk文档
        /// </summary>
        /// <param name="ret"></param>
        public void refundCallBack(string ret)
        {
            refund_thread.Abort();
            wosai_refund_btn.Enabled = true;
            own_refund_btn.Enabled = true;

            insertRecord(ret);
        }

        /// <summary>
        /// 按订单号查询，返回json字符串
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        unsafe private void query_json_btn_Click(object sender, EventArgs e)
        {
            string cmd = wosai_order_text.Text + "&" + own_order_text.Text;
            insertRecord("开始查询："+cmd);

            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            str_str query = (str_str)Invoke("_query@4", typeof(str_str));

            byte* ret_p = query(cmd);

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);

            insertRecord(ret);
        }

        /// <summary>
        /// 按订单号查询，返回用&符号连接的字符串
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        unsafe private void query_string_btn_Click(object sender, EventArgs e)
        {
            string cmd = wosai_order_text.Text + "&" + own_order_text.Text;
            insertRecord("开始查询：" + cmd);

            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            str_str query = (str_str)Invoke("_queryEx@4", typeof(str_str));

            byte* ret_p = query(cmd);

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);

            insertRecord(ret);
        }

        /// <summary>
        /// 预下单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        unsafe private void pre_create_btn_Click(object sender, EventArgs e)
        {
            int select_way = pay_way_box.SelectedIndex;
            string pay_way = select_way.ToString();

            //未指定支付方式时，传空//
            if (select_way == 0)
                pay_way = "";


            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            str_str preCreate;
            string cmd;
            if (ui_checkbox.Checked) //是否使用UI界面//
            {
                //使用ui界面会将收款二维码显示在屏幕上//
                cmd = own_order_text.Text + "&" + goods_name_text.Text + "&" + operator_text.Text
                + "&" + goods_des_text.Text + "&" + pay_way + "&" + total_amount_text.Text
                + "&" + reflect_text.Text + "&" + extern_text.Text;

                insertRecord("开始预下单：" + cmd);

                preCreate = (str_str)Invoke("_preCreateUI@4", typeof(str_str));

                byte* ret_p = preCreate(cmd);

                byte[] ret_test = new byte[1024];
                int idx = 0;
                while (*ret_p != 0)
                {
                    ret_test[idx] = (byte)(*ret_p);
                    ret_p++;
                    idx++;
                }
                ret_test[idx] = 0;

                string ret = System.Text.Encoding.Default.GetString(ret_test);

                insertRecord(ret);
            }
            else
            {
                //不使用UI界面需指定二维码图片保存路径//
                SaveFileDialog file_dialog = new SaveFileDialog();
                if (file_dialog.ShowDialog() == DialogResult.OK)
                {
                    string file_path = file_dialog.FileName.ToString();
                    cmd = own_order_text.Text + "&" + goods_name_text.Text + "&" + operator_text.Text
                + "&" + goods_des_text.Text + "&" + pay_way + "&" + total_amount_text.Text
                 + "&" + file_path + "&" + reflect_text.Text + "&" + extern_text.Text;

                    insertRecord("开始预下单：" + cmd);

                    preCreate = (str_str)Invoke("_preCreate@4", typeof(str_str));

                    byte* ret_p = preCreate(cmd);

                    byte[] ret_test = new byte[1024];
                    int idx = 0;
                    while (*ret_p != 0)
                    {
                        ret_test[idx] = (byte)(*ret_p);
                        ret_p++;
                        idx++;
                    }
                    ret_test[idx] = 0;

                    string ret = System.Text.Encoding.Default.GetString(ret_test);

                    insertRecord(ret);
                }
            }

            
        }

        /// <summary>
        /// 使用收钱吧订单号撤单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        unsafe private void wosai_revoke_btn_Click(object sender, EventArgs e)
        {
            string cmd = "";

            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

           
            if (ui_checkbox.Checked)
            {
                cmd = wosai_order_text.Text + "&" + reflect_text.Text;
            }
            else
            {
                cmd = wosai_order_text.Text + "&&" + reflect_text.Text;
            }
            insertRecord("开始撤单：" + cmd);
            
            if (revoke_thread != null)
            {
                revoke_thread = null;
            }

            if (revoke_thread == null)
            {
                wosai_revoke_btn.Enabled = false;
                own_revoke_btn.Enabled = false;
                revoke_thread = new Thread(delegate() { revoke_method(cmd,true); });
                revoke_thread.Name = "revoke thread";
                revoke_thread.Start();
            }
        }

        unsafe private void own_revoke_btn_Click(object sender, EventArgs e)
        {
            string cmd = "";

            int_int autoCodec = (int_int)Invoke("_autoCodec@4", typeof(int_int));
            autoCodec(1);

            if (ui_checkbox.Checked)
            {
                cmd = own_order_text.Text + "&" + reflect_text.Text;
            }
            else
            {
                cmd = "&" + own_order_text.Text + "&" + reflect_text.Text;
               
            }
            insertRecord("开始撤单："+cmd);

            if (revoke_thread != null)
            {
                revoke_thread = null;
            }

            if (revoke_thread == null)
            {
                own_revoke_btn.Enabled = false;
                wosai_revoke_btn.Enabled = false;
                revoke_thread = new Thread(delegate() { revoke_method(cmd, false); });
                revoke_thread.Name = "revoke thread";
                revoke_thread.Start();
            }
        }

        unsafe void revoke_method(string cmd, bool is_wosai)
        {
            str_str revoke;
            if (ui_checkbox.Checked)
            {
                if (is_wosai)
                {
                    revoke = (str_str)Invoke("_revokeUIWithSN@4", typeof(str_str));
                }
                else
                {
                    revoke = (str_str)Invoke("_revokeUIWithClientSN@4", typeof(str_str));
                }
            }
            else
            {
                revoke = (str_str)Invoke("_revoke@4", typeof(str_str));
            }
            byte* ret_p = revoke(cmd);

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);

            RevokeCallBackDelegate mi = new RevokeCallBackDelegate(revokeCallBack);
            this.BeginInvoke(mi, new object[] { ret });
        }

        public delegate void RevokeCallBackDelegate(string ret);

        /// <summary>
        /// 返回参考sdk文档
        /// </summary>
        /// <param name="ret"></param>
        public void revokeCallBack(string ret)
        {
            revoke_thread.Abort();
            wosai_revoke_btn.Enabled = true;
            own_revoke_btn.Enabled = true;

            insertRecord(ret);
        }

        /// <summary>
        /// 获取sdk版本号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        unsafe private void check_version_btn_Click(object sender, EventArgs e)
        {
            str_void version = (str_void)Invoke("_version@0", typeof(str_void));

            byte* ret_p = version();

            byte[] ret_test = new byte[1024];
            int idx = 0;
            while (*ret_p != 0)
            {
                ret_test[idx] = (byte)(*ret_p);
                ret_p++;
                idx++;
            }
            ret_test[idx] = 0;

            string ret = System.Text.Encoding.Default.GetString(ret_test);

            MessageBox.Show(ret);
        }

        /// <summary>
        /// 获取终端号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void terminal_btn_Click(object sender, EventArgs e)
        {
            if (isActivated())
            {
                MessageBox.Show(terminal_sn);
            }
            else
            {
                MessageBox.Show("未激活");
            }
        }


    }
}
