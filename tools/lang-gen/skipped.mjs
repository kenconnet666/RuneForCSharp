// 语言包里不移植的键与原因：lang-gen 跳过它们，parity 也不把它们报成缺失
export const SKIPPED = {
  errorCodes: '接口错误码的文案：桌面库不内置请求客户端（ROADMAP"平台化的约定"），应用自己的请求层按需要定义',
  formIssues: 'zod 的校验文案：三个库的校验走 INotifyDataErrorInfo / DataAnnotations，用它们自己的文案',
  messages: 'Lang 里覆盖页面级文案表的入口（createUi 的 lang.messages）：文案表本身另外生成（PatternsMessages.g.cs），改文案用 record 的 with',
};
