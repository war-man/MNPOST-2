﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MNPOST.Models;
using MNPOSTCOMMON;
using System.Globalization;
using System.Data.SqlClient;
using MNPOST.Report;
using CrystalDecisions.CrystalReports.Engine;
using System.IO;
using MNPOST.Report.customer;
namespace MNPOST.Controllers.customerdebit
{
    public class CustomerDebitNewController : BaseController
    {
        // GET: CustomerDebitNew
        public ActionResult Show()
        {
            ViewBag.allCustomer = db.BS_CustomerGroups.ToList();
            return View();
        }
        [HttpGet]
        public ActionResult GetDebit(string tungay, string denngay, string post)
        {

            string tngay = tungay.Substring(0, 2);
            string tthang = tungay.Substring(3, 2);
            string tnam = tungay.Substring(6, 4);

            string dngay = denngay.Substring(0, 2);
            string dthang = denngay.Substring(3, 2);
            string dnam = denngay.Substring(6, 4);

            string sysFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;

            //format sever M/d/yyyy
            DateTime dateFrom;
            DateTime dateTo;

            if (sysFormat == "M/d/yyyy")
            {
                dateFrom = DateTime.Parse(tthang + '/' + tngay + '/' + tnam);
                dateTo = DateTime.Parse(dthang + '/' + dngay + '/' + dnam);
            }
            else
            {
                dateFrom = DateTime.Parse(tungay);
                dateTo = DateTime.Parse(denngay);
            }
            var data = (from cdv in db.AC_CustomerDebitVoucher
                        join cg in db.BS_CustomerGroups
                        on cdv.CustomerGroupID equals cg.CustomerGroupCode
                        where cdv.PostOfficeID == post && cdv.DocumentDate >= dateFrom && cdv.DocumentDate <= dateTo
                        orderby cdv.DocumentID
                        select new
                        {
                            DocumentID = cdv.DocumentID,
                            DocumentDate = cdv.DocumentDate,
                            CustomerGroupID = cdv.CustomerGroupID,
                            CustomerName = cg.CustomerGroupName,
                            TotalAmount = cdv.ToTalAmount,
                            DebtMonth = cdv.DebtMonth,
                            StatusID = cdv.StatusID,
                            Description = cdv.Description,
                            CODAmount = cdv.CODTotal,
                            Total = (cdv.ToTalAmount - cdv.CODTotal)

                        }).ToList();
            // var data;
            ResultInfo result = new ResultInfo()
            {
                error = 0,
                msg = "",
                data = data
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult edit(List<string> kh, bool loai, int ngaychot, string thangchot, string tungay, string denngay)
        {
            return Json(new ResultInfo() { error = 0, msg = "", data = "" }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult getDetail(string documentid)
        {
            var results = (from mm in db.MM_Mailers
                           join d in db.AC_CustomerDebitVoucherDetail
                           on mm.MailerID equals d.MailerID
                           join p in db.BS_Provinces
                           on mm.RecieverProvinceID equals p.ProvinceID
                           where d.DocumentID == documentid
                           select new
                           {
                               MailerID = mm.MailerID,
                               AcceptDate = mm.AcceptDate,
                               ReciveprovinceID = p.ProvinceCode,
                               MerchandiseID = mm.MerchandiseID,
                               MailerTypeID = mm.MailerTypeID,
                               Quantity = mm.Quantity,
                               Weight = mm.Weight,
                               Price = d.Price,
                               PriceService = mm.PriceService,
                               Discount = mm.Discount,
                               Amount = mm.Amount,
                               COD = mm.COD
                           }).ToList();
            //var chiTiet = db.AC_CustomerDebitVoucherDetail.Where(p => p.DocumentID == documentid).ToList();
            return Json(results, JsonRequestBehavior.AllowGet);
        }
        public string getMaxid()
        {
            //lay ra gia tri maxid
            var maxid = db.AC_CustomerDebitVoucher.Where(p => p.PostOfficeID == "BCQ3").OrderByDescending(x => x.DocumentID).FirstOrDefault();
            string maxndg = string.Empty;
            if (maxid != null)
            {
                maxndg = string.Format("BCQ3" + "{0}", (Convert.ToUInt32(maxid.DocumentID.Substring(6)) + 1).ToString("D6"));
            }
            else
            {
                maxndg = "BCQ3" + "000001";
            }
            ResultInfo result = new ResultInfo()
            {
                error = 0,
                msg = "",
                data = maxndg
            };
            return maxndg;
        }
        [HttpPost]
        //kh: listDebit, loai: $scope.CongNo.TheoNgayChot, ngaychot: $scope.ChotNgay.Ngay, thangchot: $scope.ChotNgay.Thang, tungay: $scope.CongNo.TuNgay, denngay: $scope.CongNo.DenNgay, allcus: $scope.CongNo.AllCustomer
        public ActionResult create(List<string> kh, int ngaychot, bool allcus)
        {

            string sysFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            string customer = string.Empty;
            decimal? returncost = 0;

            //cap nhat chiet khau cho khach hang
            //exec AC_CustomerDebitVoucher_procUpdateDiscountMailer @FromDate='2018-11-28 00:00:00',@ToDate='2018-11-28 00:00:00',@PostOfficeID=N'CNTT',@PaymentMethodID=N'GN',@CustomerID=N'CNTT-2-CNTT, CNTT1-2-CNTT, HUNG-2-CNTT',@GroupByRep=0,@ByDebtDate=0
            allcus = false;
            if (allcus == true)
            {
                var listcustomer = db.BS_CustomerGroups.Select(p => p.CustomerGroupCode).ToList();
                if (listcustomer.Count == 0)
                    return Json(new ResultInfo() { error = 1, msg = "Không có khách hàng thỏa điều kiện" }, JsonRequestBehavior.AllowGet);
                foreach (var item in listcustomer)
                {
                    customer += item + ',';
                }
                customer = customer.Remove(customer.Length - 1); // bỏ đi dấu trừ cuối cùng
            }
            else if (allcus == false)
            {
                foreach (var item in kh)
                {
                    customer += item + ',';
                }
                customer = customer.Remove(customer.Length - 1); // bỏ đi dấu trừ cuối cùng
            }

            var _tungay = new SqlParameter("@FromDate", "1900.01.01");
            var _denngay = new SqlParameter("@ToDate", DateTime.Now.Date.ToString("yyyy-MM-dd"));
            var _post = new SqlParameter("@PostOfficeID", "BCQ3");
            var _payment = new SqlParameter("@PaymentMethodID", "NGTT");
            var _customerid = new SqlParameter("@CustomerID", customer);
            var _groupbyrep = new SqlParameter("@GroupByRep", false);
            var _bydebtdate = new SqlParameter("@ByDebtDate", true);
            var data = db.Database.SqlQuery<ReturnValue>("AC_CustomerDebitVoucher_procUpdateDiscountMailer @FromDate,@ToDate,@PostOfficeID,@PaymentMethodID,@CustomerID,@GroupByRep,@ByDebtDate", _tungay, _denngay, _post, _payment, _customerid, _groupbyrep, _bydebtdate).ToList();
            //ket thuc gia trị maxid
            //lưu công nợ khách hàng
            //lay ra danh sach so CG ko co trong bang ke cong no truoc do va thoai thoi gian tìm kiem

            double totalamount = 0;
            double totalcod = 0;
            if (allcus == true)
            {
                var listcustomer = db.BS_CustomerGroups.Select(p => p.CustomerGroupCode).ToList();
                foreach (var item in listcustomer)
                {
                    var groupid = db.BS_CustomerGroups.Where(p => p.CustomerGroupCode == item).Select(p => p.CustomerGroupID).FirstOrDefault();
                    var listcus = db.BS_Customers.Where(p => p.CustomerGroupID == groupid).Select(p => p.CustomerCode).ToList();
                    string maxid = getMaxid();
                    foreach (var x in listcus)
                    {
                        var _cus = new SqlParameter("@CustID", x);
                        var _fdate = new SqlParameter("@FromDate", "1900-01-01");
                        var _tdate = new SqlParameter("@ToDate", DateTime.Now.Date.ToString("yyyy-MM-dd"));
                        var results = db.Database.SqlQuery<IdentityDebit>("get_mailerfordebit @CustID,@FromDate,@ToDate", _cus, _fdate, _tdate).ToList();
                        foreach (var item1 in results)
                        {
                            if (item1.IsReturn == true)
                            {
                                var checkext = db.MM_MailerServices.Where(p => p.MailerID == item1.MailerID).FirstOrDefault();
                                if (checkext == null)
                                {
                                    returncost = item1.Price / 2; //nếu có chuyển hoàn.
                                    //thêm vào bảng dịch vụ cộng thêm
                                    MM_MailerServices ms = new MM_MailerServices();
                                    ms.MailerID = item1.MailerID;
                                    ms.ServiceID = "CH";
                                    ms.SellingPrice = decimal.Parse(returncost.ToString());
                                    ms.PriceDefault = 0;
                                    ms.IsPercentage = true;
                                    ms.BfVATAmount = decimal.Parse((double.Parse(returncost.ToString()) * 0.9).ToString());
                                    ms.VATAmount = decimal.Parse(returncost.ToString()) - decimal.Parse((double.Parse(returncost.ToString()) * 0.9).ToString());
                                    ms.AfVATAmount = decimal.Parse(returncost.ToString());
                                    ms.LastEditDate = DateTime.Now;
                                    ms.CreationDate = DateTime.Now;
                                    db.MM_MailerServices.Add(ms);
                                    //update thu khac vào bảng mm_mailers
                                    var checkmm = db.MM_Mailers.Where(p => p.MailerID == item1.MailerID).FirstOrDefault();
                                    checkmm.PriceService = checkmm.PriceService + decimal.Parse(returncost.ToString());
                                    db.Entry(checkmm).State = System.Data.Entity.EntityState.Modified;
                                    db.SaveChanges();
                                }

                            }
                            string tttt = item1.MailerID;
                            AC_CustomerDebitVoucherDetail ct = new AC_CustomerDebitVoucherDetail();
                            ct.DocumentID = maxid;
                            ct.MailerID = item1.MailerID;
                            ct.Amount = double.Parse((item1.Amount ?? 0).ToString());
                            ct.Price = decimal.Parse((double.Parse((item1.Price ?? 0).ToString()) / 1.1).ToString());
                            ct.PriceService = decimal.Parse((item1.PriceService ?? 0).ToString()) + decimal.Parse(returncost.ToString());
                            ct.DiscountPercent = decimal.Parse((item1.DiscountPercent ?? 0).ToString());
                            ct.Discount = decimal.Parse((item1.Discount ?? 0).ToString());
                            ct.VATpercent = decimal.Parse((item1.VATPercent ?? 0).ToString());
                            // ct.BfVATamount = decimal.Parse((item1.BfVATAmount ?? 0).ToString());
                            ct.BfVATamount = decimal.Round(decimal.Parse((item1.BfVATAmount ?? 0).ToString()));
                            ct.VATamount = decimal.Round(decimal.Parse((item1.VATAmount ?? 0).ToString()));
                            ct.AcceptDate = DateTime.Parse(item1.AcceptDate.ToString());
                            ct.Quantity = int.Parse(item1.Quantity.ToString());
                            ct.Weight = decimal.Parse(item1.Weight.ToString());
                            db.AC_CustomerDebitVoucherDetail.Add(ct);
                            totalamount += double.Parse((item1.Amount ?? 0).ToString());
                            totalcod += double.Parse((item1.COD ?? 0).ToString());
                            db.SaveChanges();
                        }
                        if (results.Count > 0)
                        {
                            //thông tin bảng master
                            AC_CustomerDebitVoucher mt = new AC_CustomerDebitVoucher();
                            mt.DocumentID = maxid;
                            mt.DocumentDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd"));
                            mt.PostOfficeID = "BCQ3";
                            mt.CustomerGroupID = item;
                            mt.StatusID = 0;
                            mt.ToTalAmount = Math.Round(totalamount, 0);
                            mt.CODTotal = totalcod;
                            mt.DebtMonth = DateTime.Now.Date;
                            mt.Description = "";
                            db.AC_CustomerDebitVoucher.Add(mt);
                            db.SaveChanges();
                        }
                    }
                }
            }
            else
            {
                foreach (var item in kh)
                {
                    //lay ra danh sach ma khach hang thuoc nhom khach hang
                    var groupid = db.BS_CustomerGroups.Where(p => p.CustomerGroupCode == item).Select(p => p.CustomerGroupID).FirstOrDefault();
                    var listcus = db.BS_Customers.Where(p => p.CustomerGroupID == groupid).Select(p => p.CustomerCode).ToList();
                    string maxid = getMaxid();
                    foreach (var x in listcus)
                    {
                        var _cus = new SqlParameter("@CustID", x);
                        var _fdate = new SqlParameter("@FromDate", "1900-01-01");
                        var _tdate = new SqlParameter("@ToDate", DateTime.Now.Date.ToString("yyyy-MM-dd"));
                        var results = db.Database.SqlQuery<IdentityDebit>("get_mailerfordebit @CustID,@FromDate,@ToDate", _cus, _fdate, _tdate).ToList();
                        foreach (var item1 in results)
                        {

                            if (item1.IsReturn == true)
                            {
                                var checkext = db.MM_MailerServices.Where(p => p.MailerID == item1.MailerID).FirstOrDefault();
                                if (checkext == null)
                                {
                                    returncost = item1.Price / 2; //nếu có chuyển hoàn.
                                    //thêm vào bảng dịch vụ cộng thêm
                                    MM_MailerServices ms = new MM_MailerServices();
                                    ms.MailerID = item1.MailerID;
                                    ms.ServiceID = "CH";
                                    ms.SellingPrice = decimal.Parse(returncost.ToString());
                                    ms.PriceDefault = 0;
                                    ms.IsPercentage = true;
                                    ms.BfVATAmount = decimal.Parse((double.Parse(returncost.ToString()) * 0.9).ToString());
                                    ms.VATAmount = decimal.Parse(returncost.ToString()) - decimal.Parse((double.Parse(returncost.ToString()) * 0.9).ToString());
                                    ms.AfVATAmount = decimal.Parse(returncost.ToString());
                                    ms.LastEditDate = DateTime.Now;
                                    ms.CreationDate = DateTime.Now;
                                    db.MM_MailerServices.Add(ms);

                                    var checkmm = db.MM_Mailers.Where(p => p.MailerID == item1.MailerID).FirstOrDefault();
                                    checkmm.PriceService = checkmm.PriceService + decimal.Parse(returncost.ToString());
                                    db.Entry(checkmm).State = System.Data.Entity.EntityState.Modified;
                                    db.SaveChanges();
                                }
                            }
                            string tttt = item1.MailerID;
                            AC_CustomerDebitVoucherDetail ct = new AC_CustomerDebitVoucherDetail();
                            ct.DocumentID = maxid;
                            ct.MailerID = item1.MailerID;
                            ct.Amount = double.Parse((item1.Amount ?? 0).ToString());
                            ct.Price = decimal.Parse((double.Parse((item1.Price ?? 0).ToString()) / 1.1).ToString());
                            ct.PriceService = decimal.Parse((item1.PriceService ?? 0).ToString());
                            ct.DiscountPercent = decimal.Parse((item1.DiscountPercent ?? 0).ToString());
                            ct.Discount = decimal.Parse((item1.Discount ?? 0).ToString());
                            ct.VATpercent = decimal.Parse((item1.VATPercent ?? 0).ToString());
                            // ct.BfVATamount = decimal.Parse((item1.BfVATAmount ?? 0).ToString());
                            // ct.VATamount = decimal.Parse((item1.VATAmount ?? 0).ToString());
                            ct.BfVATamount = decimal.Round(decimal.Parse((item1.BfVATAmount ?? 0).ToString()));
                            ct.VATamount = decimal.Round(decimal.Parse((item1.VATAmount ?? 0).ToString()));

                            ct.AcceptDate = DateTime.Parse(item1.AcceptDate.ToString());
                            ct.Quantity = int.Parse(item1.Quantity.ToString());
                            ct.Weight = decimal.Parse(item1.Weight.ToString());
                            db.AC_CustomerDebitVoucherDetail.Add(ct);
                            totalamount += double.Parse((item1.Amount ?? 0).ToString());
                            totalcod += double.Parse((item1.COD ?? 0).ToString());
                            db.SaveChanges();
                        }
                        if (results.Count > 0)
                        {
                            //thông tin bảng master
                            AC_CustomerDebitVoucher mt = new AC_CustomerDebitVoucher();
                            mt.DocumentID = maxid;
                            mt.DocumentDate = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd"));
                            mt.PostOfficeID = "BCQ3";
                            mt.CustomerGroupID = item;
                            mt.StatusID = 0;
                            mt.ToTalAmount = Math.Round(totalamount, 0);
                            mt.CODTotal = totalcod;
                            mt.DebtMonth = DateTime.Now.Date;
                            mt.Description = "";
                            db.AC_CustomerDebitVoucher.Add(mt);
                            db.SaveChanges();
                        }

                    }

                }
            }


            return Json(new ResultInfo() { error = 0, msg = "", data = sysFormat }, JsonRequestBehavior.AllowGet);

        }
        [HttpPost]
        public ActionResult delete(string documentid)
        {
            if (String.IsNullOrEmpty(documentid))
                return Json(new ResultInfo() { error = 1, msg = "Missing info" }, JsonRequestBehavior.AllowGet);

            var check = db.AC_CustomerDebitVoucher.Where(p => p.DocumentID == documentid).FirstOrDefault();

            if (check == null)
                return Json(new ResultInfo() { error = 1, msg = "Không tìm thấy thông tin" }, JsonRequestBehavior.AllowGet);
            var listup = db.AC_CustomerDebitVoucherDetail.Where(p => p.DocumentID == documentid).Select(p => p.MailerID).ToList();
            foreach (var item in listup)
            {
                var mailerid = db.MM_Mailers.Where(p => p.MailerID == item).FirstOrDefault();
                mailerid.DiscountPercent = 0;
                mailerid.Discount = 0;
                db.Entry(mailerid).State = System.Data.Entity.EntityState.Modified;
            }
            db.AC_CustomerDebitVoucherDetail.RemoveRange(db.AC_CustomerDebitVoucherDetail.Where(p => p.DocumentID == documentid));
            db.Entry(check).State = System.Data.Entity.EntityState.Deleted;
            db.SaveChanges();
            return Json(new ResultInfo() { error = 0, msg = "", data = check }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult thanhtoan(string documentid)
        {
            if (String.IsNullOrEmpty(documentid))
                return Json(new ResultInfo() { error = 1, msg = "Missing info" }, JsonRequestBehavior.AllowGet);

            var check = db.AC_CustomerDebitVoucher.Where(p => p.DocumentID == documentid).FirstOrDefault();
            var listcg = (from mm in db.MM_Mailers
                          join d in db.AC_CustomerDebitVoucherDetail
                          on mm.MailerID equals d.MailerID
                          where d.DocumentID == documentid
                          select new
                          {
                              MailerID = mm.MailerID,
                              COD = mm.COD
                          }).ToList();
            foreach (var item in listcg)
            {
                if (item.COD > 0)
                {
                    var checkcg = db.MM_Mailers.Where(p => p.MailerID == item.MailerID).FirstOrDefault();
                    checkcg.PaidCoD = 2;
                    db.Entry(checkcg).State = System.Data.Entity.EntityState.Modified;
                }
            }

            if (check == null)
                return Json(new ResultInfo() { error = 1, msg = "Không tìm thấy thông tin" }, JsonRequestBehavior.AllowGet);

            check.StatusID = 1;
            db.Entry(check).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();
            return Json(new ResultInfo() { error = 0, msg = "", data = check }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult getkh(int ngaychot)
        {
            var results = db.BS_CustomerGroups.Where(p => p.CODDebitDate == ngaychot).ToList();
            //var chiTiet = db.AC_CustomerDebitVoucherDetail.Where(p => p.DocumentID == documentid).ToList();
            return Json(results, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult ShowReport(string docid)
        {
            // lam như phân lấy dữ liệu thư viện
            Dictionary<string, string> textValues = new Dictionary<string, string>();
            var listmaster = db.AC_CustomerDebitVoucher.Where(p => p.DocumentID == docid).FirstOrDefault();

            textValues.Add("txtDocumentID", docid);
            textValues.Add("txtDebtMonth", DateTime.Parse(listmaster.DebtMonth.ToString()).ToString("MM/yyyy"));
            textValues.Add("txtGhiChu", listmaster.Description);
            textValues.Add("txtCustomerID", listmaster.CustomerGroupID);
            textValues.Add("txttotal", string.Format("{0:n0}", listmaster.ToTalAmount)); //tong tien

            //txttenkh
            var query =
                   (from a in db.AC_CustomerDebitVoucher
                    join b in db.BS_CustomerGroups on a.CustomerGroupID equals b.CustomerGroupCode
                    where a.DocumentID == docid
                    select b.CustomerGroupName).FirstOrDefault();
            textValues.Add("txttenkh", query);
            //
            decimal totalprice = (from a in db.AC_CustomerDebitVoucherDetail
                                  where a.DocumentID == docid
                                  select a.Price).Sum();
            textValues.Add("txttongcuoc", string.Format("{0:n0}", totalprice));

            decimal discount = (from a in db.AC_CustomerDebitVoucherDetail
                                where a.DocumentID == docid
                                select a.Discount).Sum();
            textValues.Add("txtgiamtru", string.Format("{0:n0}", discount));

            decimal vat = (from a in db.AC_CustomerDebitVoucherDetail
                           where a.DocumentID == docid
                           select a.VATamount).Sum();
            textValues.Add("txtgiamtru", string.Format("{0:n0}", discount));
            textValues.Add("txtvat", string.Format("{0:n0}", vat));

            Dictionary<string, Dictionary<string, string>> values = new Dictionary<string, Dictionary<string, string>>();
            values.Add("Section2", textValues); // 

            //lay data
            var results = (from mm in db.MM_Mailers
                           join d in db.AC_CustomerDebitVoucherDetail
                           on mm.MailerID equals d.MailerID
                           join p in db.BS_Provinces
                           on mm.RecieverProvinceID equals p.ProvinceID
                           where d.DocumentID == docid
                           select new
                           {
                               SoPhieu = mm.MailerID,
                               NgayGui = mm.AcceptDate,
                               NoiDen = p.ProvinceCode,
                               DichVu = mm.MailerTypeID,
                               SoLuong = mm.Quantity,
                               TrongLuong = mm.Weight,
                               CuocPhi = d.Price,
                               PhuPhi = d.PriceService,
                               ThanhTien = d.BfVATamount
                           }).ToList();



            Stream stream = REPORTUTILS.GetReportStream(ReportPath.RptAC_CustomerDebitDetails, results);

            return File(stream, "application/pdf");
        }
    }
}