namespace BetSniffer.Api.Core.Models
{
    public class TagInfo
    {
        // Propriedades necessárias
        public string TagName { get; set; } // Nome da tag (fixa)
        public string ElementName { get; set; } // Nome do elemento (prefixo "_ngcontent-ng-")
        public string ElementCode { get; set; } // Código dinâmico do elemento
        public string FullElement { get; set; } // HTML completo do elemento para debug

        // Construtor que aceita os parâmetros necessários
        public TagInfo(string tagName, string elementName, string elementCode, string fullElement)
        {
            TagName = tagName;
            ElementName = elementName;
            ElementCode = elementCode;
            FullElement = fullElement;
        }
    }
}
