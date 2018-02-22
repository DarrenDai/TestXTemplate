using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using XTemplate.Templating;

namespace TestXTemplate
{
    public class MainViewModel : NotifyModel
    {
        #region private fields

        private ObservableCollection<ParameterItem> _parameterList = new ObservableCollection<ParameterItem>();
        private string _templateText;
        private string _outputText;
        private string _comment;

        #endregion

        public MainViewModel()
        {
            //var temp = JsonConvert.SerializeObject("<test>\"'/\\@#$%^&*()</asdfas>");


            TemplateText =
@"<!-- 单属性示例 -->
Program Name: <#= P[""ProgramName""] #> 

<!-- 数组示例 -->
ConfigArray: <# foreach(var item in (P[""TestArray""] as List<string>))
        {  #> 
        - <#=item #>  <#  }  #>

<!-- 复杂数组示例 -->
ComplexArray: <# foreach(var item in (P[""ComplexArray""] as List<string>))
        { var items=item.Split(new string[] { ""|||"" }, StringSplitOptions.None);  #> 
        <config Name=""<#=items[0] #>"" Value=""<#=items[1] #>"" /> <#  }  #>

Config End;


";

            ParameterList.Add(new ParameterItem() { PropertyName = "ProgramName", PropertyValue = "TestProgramName" });

            var arrayParam = new ParameterItem() { PropertyName = "TestArray", DataType = "Array", PropertyValue = "AAAAA###BBBBB###CCCCC" };
            ParameterList.Add(arrayParam);

            var complexArray = new ParameterItem() { PropertyName = "ComplexArray", DataType = "Array", PropertyValue = "AAA|||NBB###BB|||BBB###CC|||DD" };
            ParameterList.Add(complexArray);

            var templateItem = new ParameterItem() { IsTemplate = true };
            templateItem.PropetyChangedEvent += OnTemplateItemChanged;
            ParameterList.Add(templateItem);

            RenderCommand = new DelegateCommand<object>(OnRender);
            SaveCommand = new DelegateCommand<object>(OnSave);
            ImportCommand = new DelegateCommand<object>(OnImport);
            DeletePropertyCommand = new DelegateCommand<object>(obj =>
            {
                try
                {
                    if (obj is ParameterItem)
                        ParameterList.Remove(obj as ParameterItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            if (Start.Args.Any())
                ImportConfigFromFile(Start.Args.First());
        }

        #region Commands

        public ICommand RenderCommand { get; set; }

        public ICommand DeletePropertyCommand { get; set; }

        public ICommand SaveCommand { get; set; }

        public ICommand ImportCommand { get; set; }

        #endregion

        #region Event handlers

        private void TextEditor_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (files.Length > 0)
            {
                string fileName = files[0];
                if (fileName.ToLower().EndsWith(".tpt"))
                {
                    ImportConfigFromFile(fileName);
                }
            }

        }

        #endregion

        #region Public Properties
        public string Comment
        {
            get { return _comment; }
            set
            {
                _comment = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ParameterItem> ParameterList { get { return _parameterList; } set { _parameterList = value; } }

        public string OutputText
        {
            get { return _outputText; }
            set { _outputText = value; OnPropertyChanged(); }
        }

        public string TemplateText
        {
            get { return _templateText; }
            set { _templateText = value; OnPropertyChanged(); }
        }

        #endregion

        #region Private methods

        private void OnRender(object payload)
        {
            try
            {
                RenderTemplate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void RenderTemplate()
        {
            //Template.Debug = true;
            //var data = new Dictionary<string, object>();
            //data.Add("ProgramName", "TestAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
            //Template.Debug = true;
            //OutputText = Template.ProcessTemplate(TemplateText, data);

            Template tt = new Template();
            tt.AddTemplateItem("testTemplate", string.Format(@"<# var P=Data[""P""] as Dictionary<string, object>; #>{0}", TemplateText));
            tt.Process();
            tt.AssemblyName = "Test";

            tt.Compile();
            TemplateBase temp = tt.CreateInstance("testTemplate");
            var P = new Dictionary<string, object>();

            foreach (var item in ParameterList.Where(x => !x.IsTemplate).ToList())
            {
                if (item.DataType == "Array")
                {
                    List<string> values = item.ValueList.Where(x => !x.IsTemplate).Select(x => x.PropertyValue).ToList();
                    P.Add(item.PropertyName, values);
                }
                else
                {
                    P.Add(item.PropertyName, item.PropertyValue);
                }
            }

            temp.Data["P"] = P;

            OutputText = temp.Render();
        }

        private void OnSave(object payload)
        {
            try
            {
                var dialog = new SaveFileDialog();
                dialog.Filter = "文本文件|*.tpt";
                if (dialog.ShowDialog().Value)
                {
                    RenderTemplate();
                    var savePath = dialog.FileName;

                    using (var fs = new FileStream(savePath, FileMode.Create))
                    {
                        using (var sw = new StreamWriter(fs))
                        {
                            sw.Write(JsonConvert.SerializeObject(
                                new
                                {
                                    Template = TemplateText,
                                    Parameters = ParameterList.Where(x => !x.IsTemplate).Select(x => (
                                                    new
                                                    {
                                                        DataType = x.DataType,
                                                        Key = x.PropertyName,
                                                        Value = x.PropertyValue,
                                                        Values = x.ValueList.Where(y => !y.IsTemplate).Select(z => z.PropertyValue).ToArray()
                                                    }
                                                    )).ToList(),
                                    Comment = Comment
                                }));
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void OnImport(object payload)
        {
            try
            {
                var dialog = new OpenFileDialog();
                dialog.Filter = "文本文件|*.tpt";
                if (dialog.ShowDialog().Value)
                {
                    var filePath = dialog.FileName;
                    ImportConfigFromFile(filePath);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void OnTemplateItemChanged()
        {
            var template = new ParameterItem() { IsTemplate = true };
            template.PropetyChangedEvent += OnTemplateItemChanged;
            _parameterList.Add(template);
        }

        public void ImportConfigFromFile(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    var content = sr.ReadToEnd();
                    JObject jObj = JsonConvert.DeserializeObject(content) as JObject;
                    TemplateText = jObj["Template"].ToString();
                    var paramArray = jObj["Parameters"] as JArray;
                    Comment = jObj["Comment"].ToString();
                    var templateItem = ParameterList.FirstOrDefault(x => x.IsTemplate);
                    ParameterList.Clear();
                    foreach (var jItem in paramArray)
                    {
                        if (jItem["DataType"].ToString().ToLower() == "array")
                        {
                            var tempParameter = new ParameterItem()
                            {
                                PropertyName = jItem["Key"].ToString(),
                                DataType = "Array"
                            };
                            var tempArrayItem = tempParameter.ValueList.FirstOrDefault();
                            tempParameter.ValueList.Clear();
                            tempParameter.ValueList.AddRange((jItem["Values"] as JArray).Select(x => new ArrayItem() { PropertyValue = x.ToString() }).ToList());
                            tempParameter.ValueList.Add(tempArrayItem);
                            ParameterList.Add(tempParameter);
                        }
                        else
                        {
                            ParameterList.Add(new ParameterItem()
                            {
                                PropertyName = jItem["Key"].ToString(),
                                PropertyValue = jItem["Value"].ToString()
                            });
                        }
                    }

                    ParameterList.Add(templateItem);
                }
            }
        }

        #endregion
    }

    public class NotifyModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged<T>(Expression<Func<T>> property)
        {
            var member = (property.Body as MemberExpression).Member as PropertyInfo;
            if (member != null)
            {
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(member.Name));
            }
        }
    }

    public class ParameterObject
    {
        public string DataType { get; set; }

        public string Key { get; set; }

        public string Value { get; set; }
    }

    public class ParameterItem : NotifyModel
    {
        #region 私有字段

        private string _propertyName = string.Empty;
        private string _propertyValue = string.Empty;
        private string _dataType = "1";
        private bool _isTemplate;
        private bool _isNullable;
        private ObservableCollection<ArrayItem> _valueList = new ObservableCollection<ArrayItem>();

        #endregion

        #region 构造函数

        public ParameterItem()
        {
            _dataType = "string";
            _valueList.Add(new ArrayItem() { IsTemplate = true });
            DeleteValueCommand = new DelegateCommand<object>(obj =>
            {
                try
                {
                    if (obj is ArrayItem)
                        _valueList.Remove(obj as ArrayItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        #endregion

        #region 事件

        public event Action PropetyChangedEvent;

        #endregion

        #region 命令

        public ICommand DeleteValueCommand { get; private set; }

        #endregion

        #region 公开属性

        public bool IsNullable
        {
            get { return _isNullable; }
            set
            {
                _isNullable = value;
                OnPropertyChanged(() => IsNullable);
            }
        }

        public bool IsArray
        {
            get { return DataType == "Array"; }
        }

        public string DataType
        {
            get { return _dataType; }
            set
            {
                if (_dataType != value)
                {
                    _dataType = value;
                    OnPropertyChanged(() => DataType);
                    OnPropertyChanged(() => IsArray);
                    if (value == "Array")
                    {
                        if (ValueList.Count == 1)
                        {
                            var tempItem = ValueList.FirstOrDefault();
                            if (string.IsNullOrEmpty(tempItem.PropertyValue))
                            {
                                ValueList.Clear();
                            }
                            else
                            {
                                tempItem.IsTemplate = false;
                            }
                            OnTemplateArraryItemChanged();
                        }
                    }
                    else
                    {
                        var tempItem = ValueList.FirstOrDefault();
                        ValueList.Clear();
                        ValueList.Add(new ArrayItem() { IsTemplate = true, PropertyValue = tempItem.PropertyValue });
                    }
                }
            }
        }

        public string PropertyName
        {
            get { return _propertyName; }
            set
            {
                if (_propertyName != value)
                {
                    _propertyName = value;
                    OnPropertyChanged(() => PropertyName);
                    ItemPropertyChanged();
                }
            }
        }

        public string PropertyValue
        {
            get
            {
                if (DataType == "Array")
                {
                    return "";
                    _propertyValue = string.Join("###", ValueList.Where(x => !x.IsTemplate).Select(x => x.PropertyValue).ToList());
                }
                else
                {
                    _propertyValue = ValueList.FirstOrDefault().PropertyValue;
                }

                return _propertyValue;
            }
            set
            {
                ValueList.Clear();

                if (DataType == "Array")
                {
                    var tempArr = Regex.Split(value, "###", RegexOptions.IgnoreCase);
                    if (!(tempArr.Count() == 1 && string.IsNullOrEmpty(tempArr[0])))
                    {
                        foreach (var item in tempArr)
                        {
                            ValueList.Add(new ArrayItem() { PropertyValue = item });
                        }
                    }

                    OnTemplateArraryItemChanged();
                }
                else
                {
                    ValueList.Add(new ArrayItem() { PropertyValue = value, IsTemplate = true });
                }

                OnPropertyChanged(() => PropertyValue);
                ItemPropertyChanged();
            }
        }

        public ObservableCollection<ArrayItem> ValueList
        {
            get { return _valueList; }
            set
            {
                _valueList = value;
                OnPropertyChanged(() => ValueList);
            }
        }

        public Dictionary<string, string> PropertyTypes
        {
            get
            {
                var list = new Dictionary<string, string>();
                list.Add("String", "string");
                //list.Add("Int", "int");
                list.Add("Array", "Array");
                return list;
            }
        }

        public bool IsTemplate
        {
            get { return _isTemplate; }
            set
            {
                _isTemplate = value;
                OnPropertyChanged(() => IsTemplate);
            }
        }

        #endregion

        #region 私有方法

        private void ItemPropertyChanged()
        {
            if (IsTemplate)
            {
                IsTemplate = false;
                if (PropetyChangedEvent != null)
                    PropetyChangedEvent.Invoke();
            }
        }

        private void OnTemplateArraryItemChanged()
        {
            var templateItem = new ArrayItem() { IsTemplate = true };
            templateItem.PropetyChangedEvent += OnTemplateArraryItemChanged;
            ValueList.Add(templateItem);
        }

        #endregion
    }

    public class ArrayItem : NotifyModel
    {
        #region 私有字段

        private string _propertyValue = string.Empty;
        private bool _isTemplate;

        #endregion

        #region 事件

        public event Action PropetyChangedEvent;

        #endregion

        #region 公开属性

        public string PropertyValue
        {
            get { return _propertyValue; }
            set
            {
                if (_propertyValue != value)
                {
                    _propertyValue = value;
                    OnPropertyChanged(() => PropertyValue);
                    ItemPropertyChanged();
                }
            }
        }

        public bool IsTemplate
        {
            get { return _isTemplate; }
            set
            {
                _isTemplate = value;
                OnPropertyChanged(() => IsTemplate);
            }
        }

        #endregion

        #region 私有方法

        private void ItemPropertyChanged()
        {
            if (IsTemplate && PropetyChangedEvent != null)
            {
                IsTemplate = false;
                if (PropetyChangedEvent != null)
                {
                    PropetyChangedEvent.Invoke();
                    PropetyChangedEvent = null;
                }
            }
        }

        #endregion
    }
}
